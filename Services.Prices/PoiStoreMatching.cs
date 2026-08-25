using MonberAPI.PoiData.Database;

namespace Services.Prices;

/// <summary>
/// Matches a discovered chain store to the nearest same-brand row in the shared `stores` table (owned by
/// Services.POI), so both services identify the same physical store with the same id. See StoreSync.
/// </summary>
internal static class PoiStoreMatching
{
    private const double MatchThresholdKm = 0.15;

    internal static long? FindNearest(DbStore[] candidates, double lat, double lon)
    {
        long? bestId = null;
        double bestDistanceKm = double.MaxValue;

        foreach (DbStore candidate in candidates)
        {
            double distanceKm = HaversineKm(lat, lon, candidate.Latitude, candidate.Longitude);
            if (distanceKm <= MatchThresholdKm && distanceKm < bestDistanceKm)
            {
                bestDistanceKm = distanceKm;
                bestId = candidate.Id;
            }
        }

        return bestId;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        double cosAngle =
            Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
            Math.Cos(DegreesToRadians(lon2) - DegreesToRadians(lon1)) +
            Math.Sin(DegreesToRadians(lat1)) * Math.Sin(DegreesToRadians(lat2));

        // Floating-point error can push cosAngle a hair outside [-1, 1] for near-identical points, which
        // would make Math.Acos return NaN.
        return 6371 * Math.Acos(Math.Clamp(cosAngle, -1, 1));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
