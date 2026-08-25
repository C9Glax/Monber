namespace MonberAPI.PoiData.Database;

/// <summary>
/// The `stores` table: owned and migrated by Services.POI only (its Id is the OpenStreetMap
/// node/way id). Services.Prices references this same project to read/join against the table
/// without owning its schema - see Services.Prices/Database/Context.cs.
/// </summary>
public record DbStore(
    long Id,
    string? Name,
    double Latitude,
    double Longitude,
    string Brand,
    string Shop,
    string? OpeningHours);
