using Microsoft.EntityFrameworkCore;
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

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

app.MapOpenApi();
app.MapScalarApiReference("/docs");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapGroup("").MapEndpoints();

await using (Context ctx = app.Services.CreateAsyncScope().ServiceProvider.GetRequiredService<Context>())
{
    await ctx.Database.MigrateAsync(CancellationToken.None);

    // Services.Prices opens the same file - WAL lets it read while this service writes, and the busy
    // timeout absorbs the brief lock contention if both services migrate at startup at the same time.
    await ctx.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", CancellationToken.None);
    await ctx.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", CancellationToken.None);

    await OverpassDataFetcher.LoadStores(ctx, CancellationToken.None);
}

app.Run();