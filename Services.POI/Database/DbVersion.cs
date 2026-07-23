namespace Services.POI.Database;

public record DbVersion(DateTimeOffset OsmBaseTimestamp, string Generator);