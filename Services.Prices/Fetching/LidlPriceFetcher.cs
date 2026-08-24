namespace Services.Prices.Fetching;

// TODO: implement scraping for Lidl's webshop/Lidl Plus app API (lidl.de).
internal class LidlPriceFetcher : IChainPriceFetcher
{
    public string Brand => "Lidl";

    public Task<ChainStorePrice[]> FetchAsync(string[] products, CancellationToken ct) =>
        Task.FromResult<ChainStorePrice[]>([]);
}
