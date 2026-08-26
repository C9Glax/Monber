namespace Services.POI;

// Tracks whether the startup Overpass sync (see OverpassDataFetcher.LoadStores) has finished, so
// OverpassSyncHealthCheck can keep /health unhealthy - and Aspire's WaitFor from the gateway
// blocked - until the initial store sync attempt has completed.
internal sealed class OverpassSyncStatus
{
    public bool IsComplete { get; private set; }

    public void MarkComplete() => IsComplete = true;
}
