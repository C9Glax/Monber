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

    public static PriceObservation ToDto(this DbPriceObservation observation, PricedStore store) => new(
        store.StoreId,
        store.Brand,
        store.Name,
        observation.Product,
        observation.Price,
        observation.Currency,
        observation.FetchedAt,
        observation.EffectiveFrom
    );
}
