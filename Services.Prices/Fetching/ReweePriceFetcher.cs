namespace Services.Prices.Fetching;

// TODO: implement scraping for REWE's webshop (rewe.de, regional pricing keyed by delivery postal code).
internal class ReweePriceFetcher : IChainPriceFetcher
{
    public string Brand => "Rewe";

    public Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct) =>
        Task.FromResult<ChainStore[]>([]);

    public Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct) =>
        Task.FromResult<ChainPrice[]>([]);
}
