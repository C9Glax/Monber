using Microsoft.AspNetCore.Http.HttpResults;
using Services.Prices.Database;
using Services.Prices.Fetching;

namespace Services.Prices.Features;

internal abstract class PostUpdatePricesEndpoint
{
    public static async Task<NoContent> Handle(Context ctx, IHttpClientFactory httpClientFactory, CancellationToken ct)
    {
        await PriceRefresher.RefreshAsync(ctx, PriceFetchers.All(httpClientFactory), TrackedProducts.All, ct);
        return TypedResults.NoContent();
    }
}
