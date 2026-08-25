namespace Services.POI.Database;

public record DbVersion(long Id, DateTimeOffset OsmBaseTimestamp, string Generator);