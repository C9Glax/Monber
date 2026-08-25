using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonberAPI.PoiData.Database;
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
                                              ) AS nearby
                                              WHERE distance_km <= {Radius}
                                              ORDER BY distance_km;
                                              """).ToArrayAsync(ct);

        long[] nearbyIds = [.. nearby.Select(s => s.Id)];
        Dictionary<long, string> externalIdsByStoreId = await ctx.StoreExternalIds
            .Where(e => nearbyIds.Contains(e.StoreId))
            .ToDictionaryAsync(e => e.StoreId, e => e.ExternalStoreId, ct);

        PricedStore[] priced = [.. nearby
            .Where(s => externalIdsByStoreId.ContainsKey(s.Id))
            .Select(s => new PricedStore(s.Id, s.Brand, s.Name, s.Latitude, s.Longitude, externalIdsByStoreId[s.Id]))];

        Dictionary<string, IChainPriceFetcher> fetchersByBrand = PriceFetchers.AllByBrand(httpClientFactory);
        PriceObservation[] result = await PriceLookup.GetPricesAsync(ctx, fetchersByBrand, priced, TrackedProducts.All, ct);

        return TypedResults.Ok(result);
    }
}
