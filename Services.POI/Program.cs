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

builder.Services.AddDbContext<Context>(opts =>
{
    opts.UseSqlite("Data Source=stores.db");
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
    await OverpassDataFetcher.LoadStores(ctx, CancellationToken.None);
}

app.Run();