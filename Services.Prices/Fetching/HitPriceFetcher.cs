namespace Services.Prices.Fetching;

// TODO: implement scraping for HIT-Markt's webshop/store-locator (hit.de).
internal class HitPriceFetcher : IChainPriceFetcher
{
    public string Brand => "HIT";

    public Task<ChainStorePrice[]> FetchAsync(string[] products, CancellationToken ct) =>
        Task.FromResult<ChainStorePrice[]>([]);
}
