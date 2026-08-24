using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Prices.Database;
using Services.Prices.Entities;
using Services.Prices.Extensions;

namespace Services.Prices.Features;

internal abstract class GetPriceHistoryEndpoint
{
    public static async Task<Results<Ok<PriceObservation[]>, NotFound>> Handle(
        Context ctx, [FromQuery(Name = "storeId")] long storeId, [FromQuery(Name = "product")] string product, CancellationToken ct)
    {
        DbStore? store = await ctx.Stores.SingleOrDefaultAsync(s => s.Id == storeId, ct);
        if (store is null)
            return TypedResults.NotFound();

        DbPriceObservation[] observations = await ctx.Prices
            .Where(p => p.StoreId == storeId && p.Product == product)
            .OrderBy(p => p.FetchedAt)
            .ToArrayAsync(ct);

        return TypedResults.Ok(observations.Select(o => o.ToDto(store)).ToArray());
    }
}
