using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonberAPI.ServiceDefaults;
using Scalar.AspNetCore;
using Services.POI;
using Services.POI.Database;
using Services.POI.Features;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddAuthorization();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Shared with Services.Prices - both services run with their own project directory as CWD, so this
// resolves to the same file at the repo root for both.
builder.Services.AddDbContext<Context>(opts =>
{
    opts.UseSqlite("Data Source=../monber.db", sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Poi"));
    opts.EnableSensitiveDataLogging();
    opts.EnableDetailedErrors();
});

// Keeps /health unhealthy - and Aspire's WaitFor from the gateway blocked - until the startup
// Overpass sync below has finished, so the gateway doesn't start proxying to POI before its store
// data is in place.
builder.Services.AddSingleton<OverpassSyncStatus>();
builder.Services.AddHealthChecks()
    .AddCheck<OverpassSyncHealthCheck>("overpass-sync");

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

app.MapOpenApi();
app.MapScalarApiReference("/docs");

// No UseHttpsRedirection() - this service is only ever reached internally, over plain http,
// via the gateway's YARP reverse proxy using service discovery (http://services-poi).
// Redirecting to the app's own https endpoint 307s every proxied request right back through
// the gateway, since YARP doesn't follow redirects, which the frontend then reports as
// "Could not reach the POI/Prices services."

app.UseAuthorization();

app.MapGroup("").MapEndpoints();

await using (Context ctx = app.Services.CreateAsyncScope().ServiceProvider.GetRequiredService<Context>())
{
    await ctx.Database.MigrateAsync(CancellationToken.None);

    // Services.Prices opens the same file - WAL lets it read while this service writes, and the busy
    // timeout absorbs the brief lock contention if both services migrate at startup at the same time.
    await ctx.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", CancellationToken.None);
    await ctx.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", CancellationToken.None);

    ILogger logger = app.Services.GetRequiredService<ILogger<Program>>();
    await OverpassDataFetcher.LoadStores(ctx, logger, CancellationToken.None);
    app.Services.GetRequiredService<OverpassSyncStatus>().MarkComplete();
}

app.Run();