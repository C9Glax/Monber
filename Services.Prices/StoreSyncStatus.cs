namespace Services.Prices;

// Tracks whether the startup store sync (see StoreSync.RunAsync) has finished, so StoreSyncHealthCheck
// can keep /health unhealthy - and Aspire's WaitFor from the gateway blocked - until the initial sync
// attempt has completed. Without this, the gateway (and any other caller) could reach the price
// endpoints before any DbStoreExternalId rows exist, and PricedStore.Query would find no priced stores
// at all - "no prices are ever returned" on a fresh stack, even though every price fetcher is healthy.
internal sealed class StoreSyncStatus
{
    public bool IsComplete { get; private set; }

    public void MarkComplete() => IsComplete = true;
}
