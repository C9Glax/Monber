using Microsoft.EntityFrameworkCore;
using Services.Prices.Database;
using Services.Prices.Fetching;

namespace Services.Prices;

internal static class PriceRefresher
{
    private const int FreshnessHours = 24;

    internal static async Task RefreshAsync(Context ctx, IChainPriceFetcher[] fetchers, string[] products, CancellationToken ct)
    {
        foreach (IChainPriceFetcher fetcher in fetchers)
        {
            DbRefreshVersion? version = await ctx.Version.FindAsync([fetcher.Brand], ct);
            if (version is not null && DateTimeOffset.UtcNow.Subtract(version.LastRefreshedAt).TotalHours < FreshnessHours)
                continue;

            ChainStorePrice[] results;
            try
            {
                results = await fetcher.FetchAsync(products, ct);
            }
            catch (Exception)
            {
                // A broken chain adapter shouldn't block the others from refreshing.
                continue;
            }

            foreach (ChainStorePrice result in results)
            {
                DbStore? store = await ctx.Stores.SingleOrDefaultAsync(
                    s => s.Brand == fetcher.Brand && s.ExternalStoreId == result.ExternalStoreId, ct);

                if (store is null)
                {
                    store = new DbStore(0, fetcher.Brand, result.ExternalStoreId, result.StoreName, result.Latitude, result.Longitude);
                    ctx.Stores.Add(store);
                    await ctx.SaveChangesAsync(ct);
                }

                ctx.Prices.Add(new DbPriceObservation(0, store.Id, result.Product, result.Price, result.Currency, DateTimeOffset.UtcNow));
            }

            if (version is null)
                ctx.Version.Add(new DbRefreshVersion(fetcher.Brand, DateTimeOffset.UtcNow));
            else
                ctx.Entry(version).CurrentValues.SetValues(version with { LastRefreshedAt = DateTimeOffset.UtcNow });

            await ctx.SaveChangesAsync(ct);
        }
    }
}
