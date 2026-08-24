using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Prices.Database;
using Services.Prices.Entities;
using Services.Prices.Fetching;

namespace Services.Prices.Features;

internal abstract class GetPricesByStoreEndpoint
{
    public static async Task<Results<Ok<PriceObservation[]>, NotFound>> Handle(
        Context ctx, IHttpClientFactory httpClientFactory,
        [FromQuery(Name = "storeId")] long storeId, CancellationToken ct)
    {
        DbStore? store = await ctx.Stores.SingleOrDefaultAsync(s => s.Id == storeId, ct);
        if (store is null)
            return TypedResults.NotFound();

        Dictionary<string, IChainPriceFetcher> fetchersByBrand = PriceFetchers.AllByBrand(httpClientFactory);
        PriceObservation[] result = await PriceLookup.GetPricesAsync(ctx, fetchersByBrand, [store], TrackedProducts.All, ct);

        return TypedResults.Ok(result);
    }
}
