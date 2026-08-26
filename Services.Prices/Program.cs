using System.Net;
using Microsoft.EntityFrameworkCore;
using MonberAPI.ServiceDefaults;
using Scalar.AspNetCore;
using Services.Prices;
using Services.Prices.Database;
using Services.Prices.Features;
using Services.Prices.Fetching;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddAuthorization();

// Keeps /health unhealthy - and Aspire's WaitFor from the gateway blocked - until the startup store
// sync below has finished, so the gateway doesn't start proxying price requests before any
// DbStoreExternalId rows exist (see StoreSyncStatus).
builder.Services.AddSingleton<StoreSyncStatus>();
builder.Services.AddHealthChecks()
    .AddCheck<StoreSyncHealthCheck>("store-sync");

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpClient(nameof(KauflandPriceFetcher))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { CookieContainer = new CookieContainer() })
    .ConfigureHttpClient(c =>
    {
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64; rv:154.0) Gecko/20100101 Firefox/154.0");
        c.DefaultRequestHeaders.Referrer = new Uri("https://filiale.kaufland.de/service/kontakt.store.html");
    });

// REWE sits behind Cloudflare; FlareSolverr (configured via FlareSolverr:Url, e.g. FlareSolverr__Url env
// var) solves the challenge and hands ReweePriceFetcher real cookies/UA to replay on this plain client.
// Both clients need a longer timeout than the service-wide default resilience handler allows: Overpass
// queries (store discovery) run up to 60s server-side, and a FlareSolverr challenge solve can take nearly
// as long since it drives a real browser.
builder.Services.AddHttpClient(nameof(ReweePriceFetcher))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false })
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(100);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
    });

builder.Services.AddHttpClient("FlareSolverr", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["FlareSolverr:Url"] ?? "http://localhost:8191");
})
    // Registered unconditionally so DI resolves even when unused - PriceFetchers only actually creates a
    // client from it (and thus dials this address) when FlareSolverrOptions.IsConfigured is true.
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(100);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
    });

// Shared with Services.POI - both services run with their own project directory as CWD, so this
// resolves to the same file at the repo root for both.
builder.Services.AddDbContext<Context>(opts =>
{
    opts.UseSqlite("Data Source=../monber.db", sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Prices"));
    opts.EnableSensitiveDataLogging();
    opts.EnableDetailedErrors();
});

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

app.MapOpenApi();
app.MapScalarApiReference("/docs");

// No UseHttpsRedirection() - this service is only ever reached internally, over plain http,
// via the gateway's YARP reverse proxy using service discovery (http://services-prices).
// Redirecting to the app's own https endpoint 307s every proxied request right back through
// the gateway, since YARP doesn't follow redirects, which the frontend then reports as
// "Could not reach the POI/Prices services."

app.UseAuthorization();

app.MapGroup("").MapEndpoints();

await using (AsyncServiceScope migrationScope = app.Services.CreateAsyncScope())
{
    Context ctx = migrationScope.ServiceProvider.GetRequiredService<Context>();
    await ctx.Database.MigrateAsync(CancellationToken.None);

    // Services.POI opens the same file - WAL lets this service read while POI writes, and the busy
    // timeout absorbs the brief lock contention if both services migrate at startup at the same time.
    await ctx.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", CancellationToken.None);
    await ctx.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", CancellationToken.None);
}

// Chain adapters (REWE in particular, via FlareSolverr) can each take up to ~100s to time out when
// their upstream is unreachable. Running this in the background - rather than awaiting it here - keeps
// the service from taking minutes to start listening whenever FlareSolverr or a chain endpoint is down.
_ = Task.Run(async () =>
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    Context ctx = scope.ServiceProvider.GetRequiredService<Context>();
    IHttpClientFactory httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

    await StoreSync.RunAsync(
        ctx, PriceFetchers.All(httpClientFactory, FlareSolverrOptions.IsConfigured(app.Configuration)), CancellationToken.None);

    app.Services.GetRequiredService<StoreSyncStatus>().MarkComplete();
});

app.Run();