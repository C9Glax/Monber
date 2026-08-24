namespace Services.Prices.Fetching;

/// <summary>
/// Discovers a chain's stores and looks up product prices at them. Each chain's webshop/store-locator is
/// different, so discovery and price lookup are owned together by a single adapter per brand.
/// Discovery is cheap/bulk and safe to run on a schedule; price lookups are per-store and expensive, so
/// they are only ever called on demand for a specific store a caller asked about.
/// </summary>
internal interface IChainPriceFetcher
{
    /// <summary>The store brand this fetcher produces prices for, e.g. "Kaufland".</summary>
    string Brand { get; }

    Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct);

    Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct);
}

internal record ChainStore(string ExternalStoreId, string? Name, double? Latitude, double? Longitude);

internal record ChainPrice(string Product, decimal Price, string Currency);
