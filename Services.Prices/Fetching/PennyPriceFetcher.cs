namespace Services.Prices.Fetching;

// TODO: implement scraping for PENNY's webshop/store-locator (penny.de).
internal class PennyPriceFetcher : IChainPriceFetcher
{
    public string Brand => "Penny";

    public Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct) =>
        Task.FromResult<ChainStore[]>([]);

    public Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct) =>
        Task.FromResult<ChainPrice[]>([]);
}
