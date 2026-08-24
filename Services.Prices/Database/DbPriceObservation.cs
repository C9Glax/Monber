namespace Services.Prices.Database;

internal record DbPriceObservation(
    long Id,
    long StoreId,
    string Product,
    decimal Price,
    string Currency,
    DateTimeOffset FetchedAt);
