namespace Services.Prices.Fetching;

// TODO: implement scraping for Netto Marken-Discount's webshop/store-locator (netto-online.de).
internal class NettoPriceFetcher : IChainPriceFetcher
{
    public string Brand => "Netto";

    public Task<ChainStorePrice[]> FetchAsync(string[] products, CancellationToken ct) =>
        Task.FromResult<ChainStorePrice[]>([]);
}
