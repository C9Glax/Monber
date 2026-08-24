namespace Services.Prices.Fetching;

/// <summary>
/// Discovers a chain's stores and looks up product prices at them. Each chain's webshop/store-locator is
/// different, so discovery and price lookup are owned together by a single adapter per brand.
/// </summary>
internal interface IChainPriceFetcher
{
    /// <summary>The store brand this fetcher produces prices for, e.g. "Kaufland".</summary>
    string Brand { get; }

    Task<ChainStorePrice[]> FetchAsync(string[] products, CancellationToken ct);
}

internal record ChainStorePrice(
    string ExternalStoreId,
    string? StoreName,
    double? Latitude,
    double? Longitude,
    string Product,
    decimal Price,
    string Currency);
