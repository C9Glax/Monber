using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Services.Prices.Database;
using Services.Prices.Fetching;

namespace Services.Prices.Features;

internal abstract class PostUpdateStoresEndpoint
{
    public static async Task<NoContent> Handle(
        Context ctx, IHttpClientFactory httpClientFactory, IConfiguration configuration,
        ILogger<PostUpdateStoresEndpoint> logger, CancellationToken ct)
    {
        await StoreSync.RunAsync(
            ctx, PriceFetchers.All(httpClientFactory, FlareSolverrOptions.IsConfigured(configuration)), logger, ct);
        return TypedResults.NoContent();
    }
}
