namespace Services.Prices.Fetching;

// TODO: implement scraping for HIT-Markt's webshop/store-locator (hit.de).
internal class HitPriceFetcher : IChainPriceFetcher
{
    public string Brand => "HIT";

    public Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct) =>
        Task.FromResult<ChainStore[]>([]);

    public Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct) =>
        Task.FromResult<ChainPrice[]>([]);
}
