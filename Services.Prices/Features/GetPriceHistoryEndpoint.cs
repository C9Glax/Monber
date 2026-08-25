using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Prices;
using Services.Prices.Database;
using Services.Prices.Entities;
using Services.Prices.Extensions;

namespace Services.Prices.Features;

internal abstract class GetPriceHistoryEndpoint
{
    public static async Task<Results<Ok<PriceObservation[]>, NotFound>> Handle(
        Context ctx, [FromQuery(Name = "storeId")] long storeId, [FromQuery(Name = "product")] string product, CancellationToken ct)
    {
        PricedStore? store = await PricedStore.Query(ctx, storeId: storeId).SingleOrDefaultAsync(ct);
        if (store is null)
            return TypedResults.NotFound();

        // SQLite/EF can't translate ORDER BY on a DateTimeOffset column, so order client-side.
        DbPriceObservation[] observations = await ctx.Prices
            .Where(p => p.StoreId == storeId && p.Product == product)
            .ToArrayAsync(ct);

        return TypedResults.Ok(observations.OrderBy(o => o.FetchedAt).Select(o => o.ToDto(store)).ToArray());
    }
}
