namespace Services.Prices.Fetching;

// TODO: implement scraping for Netto Marken-Discount's webshop/store-locator (netto-online.de).
internal class NettoPriceFetcher : IChainPriceFetcher
{
    public string Brand => "Netto";

    public Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct) =>
        Task.FromResult<ChainStore[]>([]);

    public Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct) =>
        Task.FromResult<ChainPrice[]>([]);
}
