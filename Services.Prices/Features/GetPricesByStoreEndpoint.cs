using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Prices;
using Services.Prices.Database;
using Services.Prices.Entities;
using Services.Prices.Fetching;

namespace Services.Prices.Features;

internal abstract class GetPricesByStoreEndpoint
{
    public static async Task<Results<Ok<PriceObservation[]>, NotFound>> Handle(
        Context ctx, IHttpClientFactory httpClientFactory, IConfiguration configuration,
        ILogger<GetPricesByStoreEndpoint> logger,
        [FromQuery(Name = "storeId")] long storeId, CancellationToken ct)
    {
        PricedStore? store = await PricedStore.Query(ctx, storeId: storeId).SingleOrDefaultAsync(ct);
        if (store is null)
            return TypedResults.NotFound();

        Dictionary<string, IChainPriceFetcher> fetchersByBrand = PriceFetchers.AllByBrand(
            httpClientFactory, FlareSolverrOptions.IsConfigured(configuration));
        PriceObservation[] result = await PriceLookup.GetPricesAsync(ctx, fetchersByBrand, [store], TrackedProducts.All, logger, ct);

        return TypedResults.Ok(result);
    }
}
