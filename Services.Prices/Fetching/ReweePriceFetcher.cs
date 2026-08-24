namespace Services.Prices.Fetching;

// TODO: implement scraping for REWE's webshop (rewe.de, regional pricing keyed by delivery postal code).
internal class ReweePriceFetcher : IChainPriceFetcher
{
    public string Brand => "Rewe";

    public Task<ChainStorePrice[]> FetchAsync(string[] products, CancellationToken ct) =>
        Task.FromResult<ChainStorePrice[]>([]);
}
