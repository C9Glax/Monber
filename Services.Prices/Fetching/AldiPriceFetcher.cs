namespace Services.Prices.Fetching;

// TODO: implement scraping for ALDI's webshop (aldi-nord.de / aldi-sued.de - Germany is split between two
// separate ALDI companies with separate sites; brand alone isn't enough to pick which one a store belongs to).
internal class AldiPriceFetcher : IChainPriceFetcher
{
    public string Brand => "ALDI";

    public Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct) =>
        Task.FromResult<ChainStore[]>([]);

    public Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct) =>
        Task.FromResult<ChainPrice[]>([]);
}
