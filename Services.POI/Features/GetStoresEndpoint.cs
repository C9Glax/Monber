using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonberAPI.PoiData.Database;
using Services.POI.Database;
using Services.POI.Entities;
using Services.POI.Extensions;

namespace Services.POI.Features;

internal abstract class GetStoresEndpoint
{
    private const float Radius = 30;
    public static async Task<Ok<Store[]>> Handle(Context ctx, [FromQuery(Name = "lat")]float lat, [FromQuery(Name = "lon")]float lon, CancellationToken ct)
    {
        DbStore[] stores = await ctx.Stores.FromSql($"""
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
        
        Store[] result = stores.Select(s => s.ToDto()).ToArray();

        return TypedResults.Ok(result);
    }
}