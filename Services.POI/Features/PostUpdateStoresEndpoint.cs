using Microsoft.AspNetCore.Http.HttpResults;
using Services.POI.Database;

namespace Services.POI.Features;

internal abstract class PostUpdateStoresEndpoint
{
    public static async Task<Ok> Handle(Context ctx, ILogger<PostUpdateStoresEndpoint> logger, CancellationToken ct)
    {
        await OverpassDataFetcher.LoadStores(ctx, logger, ct);
        return TypedResults.Ok();
    }
}