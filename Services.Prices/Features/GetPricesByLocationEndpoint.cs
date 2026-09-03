using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonberAPI.PoiData.Database;
using Services.Prices;
using Services.Prices.Database;
using Services.Prices.Entities;
using Services.Prices.Fetching;

namespace Services.Prices.Features;

internal abstract class GetPricesByLocationEndpoint
{
    private const float Radius = 10;

    // Matches the camelCase convention every other endpoint gets for free via minimal APIs'
    // TypedResults.Ok<T> - this endpoint writes to the response body directly, bypassing that,
    // so it needs its own JsonSerializerOptions to keep the wire format consistent.
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Streams one NDJSON <see cref="PriceStreamEvent"/> line per nearby store as soon as that store's
    /// prices are resolved, rather than buffering the whole (potentially slow, per-store live-fetch)
    /// result before responding - lets the client show stores immediately and fill in prices as they
    /// arrive instead of blocking on the slowest store in range.
    /// </summary>
    public static async Task Handle(
        HttpResponse response, Context ctx, IHttpClientFactory httpClientFactory, IConfiguration configuration,
        ILogger<GetPricesByLocationEndpoint> logger,
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

        Dictionary<string, IChainPriceFetcher> fetchersByBrand = PriceFetchers.AllByBrand(
            httpClientFactory, FlareSolverrOptions.IsConfigured(configuration));

        response.ContentType = "application/x-ndjson";

        async Task WriteEventAsync(PriceStreamEvent evt)
        {
            await JsonSerializer.SerializeAsync(response.Body, evt, StreamJsonOptions, ct);
            await response.Body.WriteAsync("\n"u8.ToArray(), ct);
            await response.Body.FlushAsync(ct);
        }

        // Stores without a chain mapping yet (StoreSync hasn't matched them to this brand's own
        // store list, which happens on its own ~15min background cycle) can't be live-fetched at
        // all - report them as checked/no-price immediately instead of omitting them from the
        // stream, so the client doesn't spin on them forever waiting for an event that never comes.
        foreach (DbStore store in nearby.Where(s => !externalIdsByStoreId.ContainsKey(s.Id)))
            await WriteEventAsync(new PriceStreamEvent(store.Id, false, []));

        await foreach (PriceStreamEvent evt in PriceLookup.StreamPricesAsync(ctx, fetchersByBrand, priced, TrackedProducts.All, logger, ct))
            await WriteEventAsync(evt);
    }
}
