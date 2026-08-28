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

            try
            {
                DbStoreExternalId[] existingMappings = await ctx.StoreExternalIds
                    .Where(e => e.Brand == fetcher.Brand)
                    .ToArrayAsync(ct);
                Dictionary<string, DbStoreExternalId> existingByExternalId = existingMappings.ToDictionary(e => e.ExternalStoreId);

                // StoreId carries a global unique constraint (one external mapping per shared store), but a
                // chain's own store list can list two entries at the same physical location that both
                // nearest-match the same POI store (confirmed live: HIT's main market and its attached
                // beverage sub-shop share an address) - the first match in a run claims that StoreId, the
                // rest are skipped rather than failing the whole batch.
                HashSet<long> claimedStoreIds = [.. existingMappings.Select(e => e.StoreId)];

                foreach (ChainStore store in stores)
                {
                    if (store.Latitude is not { } lat || store.Longitude is not { } lon)
                        continue;

                    long? matchedStoreId = PoiStoreMatching.FindNearest(candidates, lat, lon);
                    if (matchedStoreId is not { } storeId)
                        continue;

                    existingByExternalId.TryGetValue(store.ExternalStoreId, out DbStoreExternalId? existing);
                    if (existing is not null && existing.StoreId == storeId)
                        continue; // Already mapped to this store.

                    if (claimedStoreIds.Contains(storeId))
                        continue; // Another external id already claimed this shared store in this run.

                    if (existing is null)
                        ctx.StoreExternalIds.Add(new DbStoreExternalId(fetcher.Brand, store.ExternalStoreId, storeId));
                    else
                        ctx.Entry(existing).CurrentValues.SetValues(existing with { StoreId = storeId });

                    claimedStoreIds.Add(storeId);
                }

                if (version is null)
                    ctx.Version.Add(new DbRefreshVersion(fetcher.Brand, DateTimeOffset.UtcNow));
                else
                    ctx.Entry(version).CurrentValues.SetValues(version with { LastRefreshedAt = DateTimeOffset.UtcNow });

                await ctx.SaveChangesAsync(ct);
            }
            catch (Exception)
            {
                // A broken chain adapter (bad data, transient DB conflict) shouldn't block the others
                // from syncing or crash the whole background sync task - see Program.cs's Task.Run.
                ctx.ChangeTracker.Clear();
            }
        }
    }
}
