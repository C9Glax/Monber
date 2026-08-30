using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

// HIT selects a store via a Set-Cookie: mein-markt=<id> cookie (see HitPriceFetcher) - same shape as
// Kaufland's session cookie, so it needs the same per-client CookieContainer to carry it across requests.
builder.Services.AddHttpClient(nameof(HitPriceFetcher))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { CookieContainer = new CookieContainer() })
    .ConfigureHttpClient(c =>
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64; rv:154.0) Gecko/20100101 Firefox/154.0"));

builder.Services.AddHttpClient(nameof(NettoPriceFetcher));
builder.Services.AddHttpClient(nameof(PennyPriceFetcher));

// EDEKA, Lidl and Aldi Süd's store discovery goes through Overpass/OSM (see each fetcher's doc-comment),
// same as Rewe's - Overpass queries run up to 60s server-side, so these need Rewe's longer timeout too.
builder.Services.AddHttpClient(nameof(EdekaPriceFetcher))
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(100);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
    });
builder.Services.AddHttpClient(nameof(LidlPriceFetcher))
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(100);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
    });
builder.Services.AddHttpClient(nameof(AldiNordPriceFetcher));
builder.Services.AddHttpClient(nameof(AldiSuedPriceFetcher))
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(100);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
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

// Shared with Services.POI - both services run with their own project directory as CWD locally, so
// this default resolves to the same file at the repo root for both. In docker-compose (separate
// containers, no shared CWD) AppHost overrides this via the ConnectionStrings__MonberDb env var to
// point both containers at a common bind-mounted volume instead.
string connectionString = builder.Configuration.GetConnectionString("MonberDb") ?? "Data Source=../monber.db";

builder.Services.AddDbContext<Context>(opts =>
{
    opts.UseSqlite(connectionString, sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Prices"));
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
//
// Runs on a loop, not just once at startup: docker-compose only waits for Services.POI's container to
// have *started*, not for its own Overpass sync to finish populating the shared `stores` table (there's
// no compose-level healthcheck to gate on). A brand POI hasn't synced yet at the moment this runs simply
// has 0 candidate rows that pass - see StoreSync's 0-candidates branch - and a one-shot run would leave
// that brand's price lookups permanently broken until someone manually hit POST /stores/update. Retrying
// periodically lets a lost startup race self-heal on the next tick instead.
_ = Task.Run(async () =>
{
    try
    {
        PeriodicTimer timer = new(TimeSpan.FromMinutes(15));
        bool startupSyncDone = false;
        do
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            Context ctx = scope.ServiceProvider.GetRequiredService<Context>();
            IHttpClientFactory httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            ILogger<Program> storeSyncLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await StoreSync.RunAsync(
                ctx, PriceFetchers.All(httpClientFactory, FlareSolverrOptions.IsConfigured(app.Configuration)),
                storeSyncLogger, app.Lifetime.ApplicationStopping);

            if (!startupSyncDone)
            {
                app.Services.GetRequiredService<StoreSyncStatus>().MarkComplete();
                startupSyncDone = true;
            }
        } while (await timer.WaitForNextTickAsync(app.Lifetime.ApplicationStopping));
    }
    catch (OperationCanceledException)
    {
        // Expected on graceful shutdown (ApplicationStopping fired) - nothing to clean up.
    }
});

app.Run();