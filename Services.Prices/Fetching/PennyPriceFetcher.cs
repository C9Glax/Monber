namespace Services.Prices.Fetching;

// TODO: implement scraping for PENNY's webshop/store-locator (penny.de).
internal class PennyPriceFetcher : IChainPriceFetcher
{
    public string Brand => "Penny";

    public Task<ChainStorePrice[]> FetchAsync(string[] products, CancellationToken ct) =>
        Task.FromResult<ChainStorePrice[]>([]);
}
