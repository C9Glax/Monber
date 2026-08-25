using Microsoft.EntityFrameworkCore;
using Services.Prices.Database;

namespace Services.Prices;

/// <summary>
/// A store Services.Prices can fetch/report prices for: the shared POI/OSM store id and location,
/// plus the chain-specific identifier needed to call that chain's own site (see DbStoreExternalId).
/// Only stores with a resolved DbStoreExternalId mapping are queryable this way - see StoreSync.
/// </summary>
internal record PricedStore(long StoreId, string Brand, string? Name, double Latitude, double Longitude, string ExternalStoreId)
{
    /// <summary>
    /// Filter by <paramref name="brand"/>/<paramref name="storeId"/> here, on the raw joined columns,
    /// rather than with a `.Where`/`.SingleOrDefaultAsync(predicate)` on the resulting query - EF can't
    /// translate a filter against a property of the already-constructed PricedStore record.
    /// </summary>
    internal static IQueryable<PricedStore> Query(Context ctx, string? brand = null, long? storeId = null) =>
        from store in ctx.Stores
        where (brand == null || store.Brand == brand) && (storeId == null || store.Id == storeId)
        join link in ctx.StoreExternalIds on store.Id equals link.StoreId
        select new PricedStore(store.Id, store.Brand, store.Name, store.Latitude, store.Longitude, link.ExternalStoreId);
}
