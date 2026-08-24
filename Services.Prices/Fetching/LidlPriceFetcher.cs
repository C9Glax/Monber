namespace Services.Prices.Fetching;

// TODO: implement scraping for Lidl's webshop/Lidl Plus app API (lidl.de).
internal class LidlPriceFetcher : IChainPriceFetcher
{
    public string Brand => "Lidl";

    public Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct) =>
        Task.FromResult<ChainStore[]>([]);

    public Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct) =>
        Task.FromResult<ChainPrice[]>([]);
}
