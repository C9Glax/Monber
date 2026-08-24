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
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(100);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
    });

builder.Services.AddDbContext<Context>(opts =>
{
    opts.UseSqlite("Data Source=prices.db");
    opts.EnableSensitiveDataLogging();
    opts.EnableDetailedErrors();
});

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

app.MapOpenApi();
app.MapScalarApiReference("/docs");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapGroup("").MapEndpoints();

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    Context ctx = scope.ServiceProvider.GetRequiredService<Context>();
    IHttpClientFactory httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

    await ctx.Database.MigrateAsync(CancellationToken.None);
    await StoreSync.RunAsync(ctx, PriceFetchers.All(httpClientFactory), CancellationToken.None);
}

app.Run();