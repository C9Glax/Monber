using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Prices.Database;
using Services.Prices.Entities;
using Services.Prices.Extensions;
using Services.Prices.Fetching;

namespace Services.Prices;

/// <summary>
/// Fetches prices on demand for a specific set of stores, caching per store-local calendar day (see
/// <see cref="StoreClock"/>): if a price for a store/product was already fetched today, it's served
/// from the DB; otherwise it's fetched live from the chain's own site and appended as a new history
/// row. A price is only ever served on the store-local day it was fetched - an older one is expired
/// and withheld, even when today's live fetch fails, rather than being passed off as current.
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
        foreach (PricedStore store in stores)
        {
            // Resolved per store, not once for the whole request: the day a price belongs to is the
            // store's local day, and a request can straddle local midnight (or, later, stores in more
            // than one zone) - re-reading it here keeps every store judged against its own clock.
            DateOnly today = StoreClock.Today(store);

            DbPriceObservation[] existing = await ctx.Prices
                .Where(p => p.StoreId == store.StoreId && products.Contains(p.Product))
                .ToArrayAsync(ct);

            Dictionary<string, DbPriceCheck> checksByProduct = await ctx.PriceChecks
                .Where(c => c.StoreId == store.StoreId && products.Contains(c.Product))
                .ToDictionaryAsync(c => c.Product, ct);

            // Freshness is tracked separately from observations (DbPriceCheck), not derived from
            // whether a price was found - a product a store doesn't stock still needs to count as
            // "checked today" or it would be re-fetched live on every single request forever.
            // A calendar-day comparison, deliberately not a rolling "less than 24h old" window: a check
            // at 23:59 and one at 00:01 the next day are different days, so the second one refetches.
            string[] fresh = [.. checksByProduct
                .Where(kv => StoreClock.LocalDate(store, kv.Value.LastCheckedAt) == today)
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
                    // Fall through and serve whatever was already cached for this store today. Anything
                    // cached on an earlier day is expired and gets filtered out below, so a failed
                    // fetch degrades to "no price" rather than to a stale one.
                    logger.LogWarning(ex,
                        "Live price fetch failed for {Brand} store {StoreId}; serving today's cached prices instead",
                        store.Brand, store.StoreId);
                }
            }

            PriceObservation[] resolved = [.. existing
                // Prices expire at store-local midnight. `existing` is the full history for these
                // products (and, when the live fetch above failed, holds only older rows), so filter
                // here rather than returning yesterday's price as if it still held today.
                .Where(p => StoreClock.LocalDate(store, p.FetchedAt) == today)
                // Group by (Product, EffectiveFrom), not just Product - a current and a future price for
                // the same product are both valid, distinct rows and must not collapse into one.
                .GroupBy(p => (p.Product, p.EffectiveFrom))
                .Select(g => g.OrderByDescending(p => p.FetchedAt).First().ToDto(store, withValidity: true))];

            yield return new PriceStreamEvent(store.StoreId, resolved.Length > 0, resolved);
        }
    }
}
