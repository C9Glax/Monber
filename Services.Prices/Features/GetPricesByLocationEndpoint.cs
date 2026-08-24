using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.Prices;
using Services.Prices.Database;
using Services.Prices.Entities;
using Services.Prices.Fetching;

namespace Services.Prices.Features;

internal abstract class GetPricesByLocationEndpoint
{
    private const float Radius = 30;

    public static async Task<Ok<PriceObservation[]>> Handle(
        Context ctx, IHttpClientFactory httpClientFactory,
        [FromQuery(Name = "lat")] float lat, [FromQuery(Name = "lon")] float lon,
        CancellationToken ct)
    {
        DbStore[] nearby = await ctx.Stores.FromSql($"""
                                              SELECT *
                                              FROM (
                                                  SELECT *,
                                                         (
                                                             6371 * acos(
                                                                 cos(radians({lat})) *
                                                                 cos(radians(latitude)) *
                                                                 cos(radians(longitude) - radians({lon})) +
                                                                 sin(radians({lat})) *
                                                                 sin(radians(latitude))
                                                             )
                                                         ) AS distance_km
                                                  FROM stores
                                                  WHERE latitude IS NOT NULL AND longitude IS NOT NULL
                                              ) AS nearby
                                              WHERE distance_km <= {Radius}
                                              ORDER BY distance_km;
                                              """).ToArrayAsync(ct);

        Dictionary<string, IChainPriceFetcher> fetchersByBrand = PriceFetchers.AllByBrand(httpClientFactory);
        PriceObservation[] result = await PriceLookup.GetPricesAsync(ctx, fetchersByBrand, nearby, TrackedProducts.All, ct);

        return TypedResults.Ok(result);
    }
}
