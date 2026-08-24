namespace Services.Prices.Fetching;

internal static class PriceFetchers
{
    internal static IChainPriceFetcher[] All(IHttpClientFactory httpClientFactory) =>
    [
        new KauflandPriceFetcher(httpClientFactory.CreateClient(nameof(KauflandPriceFetcher))),
        new ReweePriceFetcher(),
        new NettoPriceFetcher(),
        new HitPriceFetcher(),
        new EdekaPriceFetcher(),
        new LidlPriceFetcher(),
        new AldiPriceFetcher(),
        new PennyPriceFetcher(),
    ];

    internal static Dictionary<string, IChainPriceFetcher> AllByBrand(IHttpClientFactory httpClientFactory) =>
        All(httpClientFactory).ToDictionary(f => f.Brand);
}
