using Microsoft.AspNetCore.Http.HttpResults;
using Services.Prices.Database;
using Services.Prices.Fetching;

namespace Services.Prices.Features;

internal abstract class PostUpdateStoresEndpoint
{
    public static async Task<NoContent> Handle(Context ctx, IHttpClientFactory httpClientFactory, CancellationToken ct)
    {
        await StoreSync.RunAsync(ctx, PriceFetchers.All(httpClientFactory), ct);
        return TypedResults.NoContent();
    }
}
