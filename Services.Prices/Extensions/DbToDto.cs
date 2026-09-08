using Services.Prices.Database;
using Services.Prices.Entities;
// PricedStore lives in the Services.Prices root namespace.
using Services.Prices;

namespace Services.Prices.Extensions;

internal static class DbToDto
{
    public static StoreSummary ToDto(this PricedStore store) => new(
        store.StoreId,
        store.Brand,
        store.Name,
        store.Latitude,
        store.Longitude
    );

    /// <param name="withValidity">
    /// Set for a price being served as current (see PriceLookup), so the client is told when it
    /// expires. Left off for raw history rows (GetPriceHistoryEndpoint), which are past observations
    /// rather than claims about what a price still is.
    /// </param>
    public static PriceObservation ToDto(this DbPriceObservation observation, PricedStore store, bool withValidity = false) => new(
        store.StoreId,
        store.Brand,
        store.Name,
        observation.Product,
        observation.Price,
        observation.Currency,
        observation.FetchedAt,
        observation.EffectiveFrom,
        observation.SourceUrl,
        withValidity ? StoreClock.ValidUntil(store, observation.FetchedAt) : null
    );
}
