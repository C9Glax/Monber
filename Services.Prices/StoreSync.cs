using Microsoft.EntityFrameworkCore;
using MonberAPI.PoiData.Database;
using Services.Prices.Database;
using Services.Prices.Fetching;

namespace Services.Prices;

/// <summary>
/// Keeps the (Brand, ExternalStoreId) -> shared store id mapping fresh. Cheap and bulk (one request per
/// chain for most adapters), so this runs on a schedule - unlike price lookups, which are only ever
/// fetched on demand (see PriceLookup). The shared `stores` table itself is owned/written only by
/// Services.POI; a chain store with no matching POI-synced location yet is simply skipped until a later
/// pass (of either service) resolves one - see PoiStoreMatching.
/// </summary>
internal static class StoreSync
{
    private const int FreshnessHours = 24;

    internal static async Task RunAsync(Context ctx, IChainPriceFetcher[] fetchers, CancellationToken ct)
    {
        foreach (IChainPriceFetcher fetcher in fetchers)
        {
            DbRefreshVersion? version = await ctx.Version.FindAsync([fetcher.Brand], ct);
            if (version is not null && DateTimeOffset.UtcNow.Subtract(version.LastRefreshedAt).TotalHours < FreshnessHours)
                continue;

            ChainStore[] stores;
            try
            {
                stores = await fetcher.DiscoverStoresAsync(ct);
            }
            catch (Exception)
            {
                // A broken chain adapter shouldn't block the others from syncing.
                continue;
            }

            DbStore[] candidates = await ctx.Stores.Where(s => s.Brand == fetcher.Brand).ToArrayAsync(ct);
            if (candidates.Length == 0)
                // Services.POI hasn't synced this brand's stores into the shared table yet (e.g. both
                // services' startup syncs are racing) - retry next run instead of marking fresh for 24h.
                continue;

            foreach (ChainStore store in stores)
            {
                if (store.Latitude is not { } lat || store.Longitude is not { } lon)
                    continue;

                long? matchedStoreId = PoiStoreMatching.FindNearest(candidates, lat, lon);
                if (matchedStoreId is not { } storeId)
                    continue;

                DbStoreExternalId? existing = await ctx.StoreExternalIds.SingleOrDefaultAsync(
                    e => e.Brand == fetcher.Brand && e.ExternalStoreId == store.ExternalStoreId, ct);

                if (existing is null)
                    ctx.StoreExternalIds.Add(new DbStoreExternalId(fetcher.Brand, store.ExternalStoreId, storeId));
                else if (existing.StoreId != storeId)
                    ctx.Entry(existing).CurrentValues.SetValues(existing with { StoreId = storeId });
            }

            if (version is null)
                ctx.Version.Add(new DbRefreshVersion(fetcher.Brand, DateTimeOffset.UtcNow));
            else
                ctx.Entry(version).CurrentValues.SetValues(version with { LastRefreshedAt = DateTimeOffset.UtcNow });

            await ctx.SaveChangesAsync(ct);
        }
    }
}
