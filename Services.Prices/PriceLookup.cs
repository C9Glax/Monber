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
        List<PriceObservation> results = [];
        await foreach (PriceStreamEvent evt in StreamPricesAsync(ctx, fetchersByBrand, stores, products, logger, ct))
            results.AddRange(evt.Observations);
        return [.. results];
    }

    /// <summary>
    /// Same lookup as <see cref="GetPricesAsync"/>, but yields one <see cref="PriceStreamEvent"/> per
    /// store as soon as that store is resolved, instead of buffering every store's result until all of
    /// them are done - lets a caller (e.g. a streaming HTTP endpoint) surface prices progressively.
    /// </summary>
    internal static async IAsyncEnumerable<PriceStreamEvent> StreamPricesAsync(
        Context ctx, IReadOnlyDictionary<string, IChainPriceFetcher> fetchersByBrand,
        PricedStore[] stores, string[] products, ILogger logger,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        foreach (PricedStore store in stores)
        {
            DbPriceObservation[] existing = await ctx.Prices
                .Where(p => p.StoreId == store.StoreId && products.Contains(p.Product))
                .ToArrayAsync(ct);

            Dictionary<string, DbPriceCheck> checksByProduct = await ctx.PriceChecks
                .Where(c => c.StoreId == store.StoreId && products.Contains(c.Product))
                .ToDictionaryAsync(c => c.Product, ct);

            // Freshness is tracked separately from observations (DbPriceCheck), not derived from
            // whether a price was found - a product a store doesn't stock still needs to count as
            // "checked today" or it would be re-fetched live on every single request forever.
            string[] fresh = [.. checksByProduct
                .Where(kv => DateOnly.FromDateTime(kv.Value.LastCheckedAt.UtcDateTime) == today)
                .Select(kv => kv.Key)];
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

                    foreach (string product in stale)
                    {
                        if (checksByProduct.TryGetValue(product, out DbPriceCheck? check))
                            ctx.Entry(check).CurrentValues.SetValues(check with { LastCheckedAt = DateTimeOffset.UtcNow });
                        else
                            ctx.PriceChecks.Add(new DbPriceCheck(store.StoreId, product, DateTimeOffset.UtcNow));
                    }

                    await ctx.SaveChangesAsync(ct);
                    if (live.Length > 0)
                        existing = [.. existing, .. ctx.Prices.Local.Where(p => p.StoreId == store.StoreId && live.Any(l => l.Product == p.Product))];

                    logger.LogInformation(
                        "Fetched {Count} live prices for {Brand} store {StoreId}",
                        live.Length, store.Brand, store.StoreId);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Genuine caller cancellation (e.g. the client changed location) - propagate
                    // instead of treating it as a per-store fetch failure to fall back from.
                    throw;
                }
                catch (Exception ex)
                {
                    // Fall through and serve whatever was already cached for this store.
                    logger.LogWarning(ex,
                        "Live price fetch failed for {Brand} store {StoreId}; serving cached prices instead",
                        store.Brand, store.StoreId);
                }
            }

            PriceObservation[] resolved = [.. existing
                // Group by (Product, EffectiveFrom), not just Product - a current and a future price for
                // the same product are both valid, distinct rows and must not collapse into one.
                .GroupBy(p => (p.Product, p.EffectiveFrom))
                .Select(g => g.OrderByDescending(p => p.FetchedAt).First().ToDto(store))];

            yield return new PriceStreamEvent(store.StoreId, resolved.Length > 0, resolved);
        }
    }
}
