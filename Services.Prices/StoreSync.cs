using Microsoft.EntityFrameworkCore;
using Services.Prices.Database;
using Services.Prices.Fetching;

namespace Services.Prices;

/// <summary>
/// Keeps the local store cache fresh. Cheap and bulk (one request per chain for most adapters), so this
/// runs on a schedule - unlike price lookups, which are only ever fetched on demand (see PriceLookup).
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

            foreach (ChainStore store in stores)
            {
                DbStore? existing = await ctx.Stores.SingleOrDefaultAsync(
                    s => s.Brand == fetcher.Brand && s.ExternalStoreId == store.ExternalStoreId, ct);

                if (existing is null)
                    ctx.Stores.Add(new DbStore(0, fetcher.Brand, store.ExternalStoreId, store.Name, store.Latitude, store.Longitude));
                else
                    ctx.Entry(existing).CurrentValues.SetValues(existing with
                    {
                        Name = store.Name,
                        Latitude = store.Latitude,
                        Longitude = store.Longitude
                    });
            }

            if (version is null)
                ctx.Version.Add(new DbRefreshVersion(fetcher.Brand, DateTimeOffset.UtcNow));
            else
                ctx.Entry(version).CurrentValues.SetValues(version with { LastRefreshedAt = DateTimeOffset.UtcNow });

            await ctx.SaveChangesAsync(ct);
        }
    }
}
