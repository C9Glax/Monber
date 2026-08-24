using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Prices.Database;
using Services.Prices.Entities;
using Services.Prices.Extensions;

namespace Services.Prices.Features;

internal abstract class GetPricesEndpoint
{
    public static async Task<Ok<PriceObservation[]>> Handle(Context ctx, [FromQuery(Name = "storeIds")] long[] storeIds, CancellationToken ct)
    {
        DbStore[] stores = await ctx.Stores.Where(s => storeIds.Contains(s.Id)).ToArrayAsync(ct);
        Dictionary<long, DbStore> storesById = stores.ToDictionary(s => s.Id);

        DbPriceObservation[] observations = await ctx.Prices
            .Where(p => storeIds.Contains(p.StoreId))
            .ToArrayAsync(ct);

        PriceObservation[] latest = observations
            .GroupBy(p => (p.StoreId, p.Product))
            .Select(g => g.OrderByDescending(p => p.FetchedAt).First())
            .Where(p => storesById.ContainsKey(p.StoreId))
            .Select(p => p.ToDto(storesById[p.StoreId]))
            .ToArray();

        return TypedResults.Ok(latest);
    }
}
