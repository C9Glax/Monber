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

builder.Services.AddHttpClient(nameof(KauflandPriceFetcher));

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
    await PriceRefresher.RefreshAsync(ctx, PriceFetchers.All(httpClientFactory), TrackedProducts.All, CancellationToken.None);
}

app.Run();