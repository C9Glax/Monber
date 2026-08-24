namespace Services.Prices.Fetching;

// TODO: implement scraping for EDEKA's webshop/store-locator (edeka.de) - franchise-based, regional pricing.
internal class EdekaPriceFetcher : IChainPriceFetcher
{
    public string Brand => "EDEKA";

    public Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct) =>
        Task.FromResult<ChainStore[]>([]);

    public Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct) =>
        Task.FromResult<ChainPrice[]>([]);
}
