using MonberAPI.PoiData.Database;
using Services.Prices.Fetching;

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

    /// <summary>
    /// The reverse of <see cref="FindNearest"/>: given a POI store's own location, finds the nearest
    /// same-brand chain store from a live <see cref="IChainPriceFetcher.DiscoverStoresAsync"/> result.
    /// Used to resolve a chain's external store id on demand for a single store that the scheduled
    /// <see cref="StoreSync"/> pass hasn't matched yet (e.g. just discovered, or its last sync predates
    /// this store), rather than making a force-refresh wait on the next scheduled sync.
    /// </summary>
    internal static ChainStore? FindNearestChainStore(ChainStore[] chainStores, double lat, double lon)
    {
        ChainStore? best = null;
        double bestDistanceKm = double.MaxValue;

        foreach (ChainStore candidate in chainStores)
        {
            if (candidate.Latitude is not { } candidateLat || candidate.Longitude is not { } candidateLon)
                continue;

            double distanceKm = HaversineKm(lat, lon, candidateLat, candidateLon);
            if (distanceKm <= MatchThresholdKm && distanceKm < bestDistanceKm)
            {
                bestDistanceKm = distanceKm;
                best = candidate;
            }
        }

        return best;
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
