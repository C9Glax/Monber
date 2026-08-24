using Services.Prices.Database;
using Services.Prices.Entities;

namespace Services.Prices.Extensions;

internal static class DbToDto
{
    public static StoreSummary ToDto(this DbStore store) => new(
        store.Id,
        store.Brand,
        store.Name,
        store.Latitude,
        store.Longitude
    );

    public static PriceObservation ToDto(this DbPriceObservation observation, DbStore store) => new(
        store.Id,
        store.Brand,
        store.Name,
        observation.Product,
        observation.Price,
        observation.Currency,
        observation.FetchedAt
    );
}
