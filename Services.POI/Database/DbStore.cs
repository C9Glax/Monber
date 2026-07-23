namespace Services.POI.Database;

internal record DbStore(
    long Id,
    string? Name,
    double Latitude,
    double Longitude,
    string Brand,
    string Shop,
    string? OpeningHours);