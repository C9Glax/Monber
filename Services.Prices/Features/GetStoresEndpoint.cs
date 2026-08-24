using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Prices.Database;
using Services.Prices.Entities;
using Services.Prices.Extensions;

namespace Services.Prices.Features;

internal abstract class GetStoresEndpoint
{
    public static async Task<Ok<StoreSummary[]>> Handle(Context ctx, [FromQuery(Name = "brand")] string? brand, CancellationToken ct)
    {
        DbStore[] stores = await ctx.Stores
            .Where(s => brand == null || s.Brand == brand)
            .ToArrayAsync(ct);

        return TypedResults.Ok(stores.Select(s => s.ToDto()).ToArray());
    }
}
