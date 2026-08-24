namespace Services.Prices.Database;

internal record DbStore(
    long Id,
    string Brand,
    string ExternalStoreId,
    string? Name,
    double? Latitude,
    double? Longitude);
