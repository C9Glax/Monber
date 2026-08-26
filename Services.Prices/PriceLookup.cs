using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Prices.Database;
using Services.Prices.Entities;
using Services.Prices.Extensions;
using Services.Prices.Fetching;

namespace Services.Prices;

/// <summary>
/// Fetches prices on demand for a specific set of stores, caching per calendar day: if a price for a
/// store/product was already fetched today, it's served from the DB; otherwise it's fetched live from
/// the chain's own site and appended as a new history row.
/// </summary>
internal static class PriceLookup
{
    internal static async Task<PriceObservation[]> GetPricesAsync(
        Context ctx, IReadOnlyDictionary<string, IChainPriceFetcher> fetchersByBrand,
        PricedStore[] stores, string[] products, ILogger logger, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        List<PriceObservation> results = [];

        foreach (PricedStore store in stores)
        {
            DbPriceObservation[] existing = await ctx.Prices
                .Where(p => p.StoreId == store.StoreId && products.Contains(p.Product))
                .ToArrayAsync(ct);

            string[] fresh = [.. existing
                .Where(p => DateOnly.FromDateTime(p.FetchedAt.UtcDateTime) == today)
                .Select(p => p.Product)];
            string[] stale = [.. products.Except(fresh)];

            if (stale.Length > 0 && fetchersByBrand.TryGetValue(store.Brand, out IChainPriceFetcher? fetcher))
            {
                ChainStore chainStore = new(store.ExternalStoreId, store.Name, store.Latitude, store.Longitude);
                logger.LogInformation(
                    "Fetching live prices for {Brand} store {StoreId} ({StaleCount} stale products)",
                    store.Brand, store.StoreId, stale.Length);
                try
                {
                    ChainPrice[] live = await fetcher.FetchPricesAsync(chainStore, stale, ct);
                    foreach (ChainPrice price in live)
                        ctx.Prices.Add(new DbPriceObservation(0, store.StoreId, price.Product, price.Price, price.Currency, DateTimeOffset.UtcNow, price.EffectiveFrom, price.SourceUrl));

                    if (live.Length > 0)
                    {
                        await ctx.SaveChangesAsync(ct);
                        existing = [.. existing, .. ctx.Prices.Local.Where(p => p.StoreId == store.StoreId && live.Any(l => l.Product == p.Product))];
                    }

                    logger.LogInformation(
                        "Fetched {Count} live prices for {Brand} store {StoreId}",
                        live.Length, store.Brand, store.StoreId);
                }
                catch (Exception ex)
                {
                    // Fall through and serve whatever was already cached for this store.
                    logger.LogWarning(ex,
                        "Live price fetch failed for {Brand} store {StoreId}; serving cached prices instead",
                        store.Brand, store.StoreId);
                }
            }

            results.AddRange(existing
                // Group by (Product, EffectiveFrom), not just Product - a current and a future price for
                // the same product are both valid, distinct rows and must not collapse into one.
                .GroupBy(p => (p.Product, p.EffectiveFrom))
                .Select(g => g.OrderByDescending(p => p.FetchedAt).First().ToDto(store)));
        }

        return [.. results];
    }
}
