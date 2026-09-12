using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonberAPI.PoiData.Database;
using Services.Prices;
using Services.Prices.Database;
using Services.Prices.Entities;
using Services.Prices.Fetching;

namespace Services.Prices.Features;

internal abstract class PostRefreshStorePricesEndpoint
{
    public static async Task<Results<Ok<PriceObservation[]>, NotFound>> Handle(
        Context ctx, IHttpClientFactory httpClientFactory, IConfiguration configuration,
        ILogger<PostRefreshStorePricesEndpoint> logger,
        [FromQuery(Name = "storeId")] long storeId, CancellationToken ct)
    {
        Dictionary<string, IChainPriceFetcher> fetchersByBrand = PriceFetchers.AllByBrand(
            httpClientFactory, FlareSolverrOptions.IsConfigured(configuration));

        PricedStore? store = await PricedStore.Query(ctx, storeId: storeId).SingleOrDefaultAsync(ct);

        // Not mapped to a chain yet - rather than 404 (which would make "force refresh" only ever work
        // for stores the scheduled StoreSync pass already happened to match), resolve the mapping right
        // now against the chain's own store list so the user-triggered refresh actually reaches the
        // price fetcher instead of waiting on the next sync cycle.
        store ??= await ResolveOnDemandAsync(ctx, storeId, fetchersByBrand, logger, ct);
        if (store is null)
            return TypedResults.NotFound();

        PriceObservation[] result = await PriceLookup.GetPricesAsync(
            ctx, fetchersByBrand, [store], TrackedProducts.All, logger, ct, forceRefresh: true);

        return TypedResults.Ok(result);
    }

    private static async Task<PricedStore?> ResolveOnDemandAsync(
        Context ctx, long storeId, IReadOnlyDictionary<string, IChainPriceFetcher> fetchersByBrand,
        ILogger logger, CancellationToken ct)
    {
        DbStore? poiStore = await ctx.Stores.FindAsync([storeId], ct);
        if (poiStore is null || !fetchersByBrand.TryGetValue(poiStore.Brand, out IChainPriceFetcher? fetcher))
            return null;

        ChainStore[] discovered;
        try
        {
            discovered = await fetcher.DiscoverStoresAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "On-demand store discovery failed for {Brand} store {StoreId}", poiStore.Brand, storeId);
            return null;
        }

        ChainStore? match = PoiStoreMatching.FindNearestChainStore(discovered, poiStore.Latitude, poiStore.Longitude);
        if (match is null)
            return null;

        // The unique index on DbStoreExternalId.StoreId means a concurrent StoreSync/refresh for the
        // same store can race this insert - fall back to whatever mapping won instead of failing the
        // request over a mapping that already exists and is perfectly usable.
        try
        {
            ctx.StoreExternalIds.Add(new DbStoreExternalId(poiStore.Brand, match.ExternalStoreId, storeId));
            await ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            ctx.ChangeTracker.Clear();
            DbStoreExternalId? existing = await ctx.StoreExternalIds.FirstOrDefaultAsync(e => e.StoreId == storeId, ct);
            if (existing is null)
                throw;
            match = match with { ExternalStoreId = existing.ExternalStoreId };
        }

        return new PricedStore(storeId, poiStore.Brand, poiStore.Name, poiStore.Latitude, poiStore.Longitude, match.ExternalStoreId);
    }
}
