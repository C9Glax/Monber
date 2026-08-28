namespace Services.Prices.Fetching;

internal static class PriceFetchers
{
    /// <summary>
    /// ReweePriceFetcher is the only adapter that depends on FlareSolverr (see FlareSolverrClient) - without
    /// it, every store sync/price lookup would attempt and fail a Cloudflare-solve against an unreachable
    /// FlareSolverr instance, so it's only included when FlareSolverr:Url is actually configured.
    /// </summary>
    internal static IChainPriceFetcher[] All(IHttpClientFactory httpClientFactory, bool flareSolverrConfigured)
    {
        List<IChainPriceFetcher> fetchers =
        [
            new KauflandPriceFetcher(httpClientFactory.CreateClient(nameof(KauflandPriceFetcher))),
            new NettoPriceFetcher(httpClientFactory.CreateClient(nameof(NettoPriceFetcher))),
            new HitPriceFetcher(httpClientFactory.CreateClient(nameof(HitPriceFetcher))),
            new EdekaPriceFetcher(httpClientFactory.CreateClient(nameof(EdekaPriceFetcher))),
            new LidlPriceFetcher(httpClientFactory.CreateClient(nameof(LidlPriceFetcher))),
            new AldiNordPriceFetcher(httpClientFactory.CreateClient(nameof(AldiNordPriceFetcher))),
            new AldiSuedPriceFetcher(httpClientFactory.CreateClient(nameof(AldiSuedPriceFetcher))),
            new PennyPriceFetcher(httpClientFactory.CreateClient(nameof(PennyPriceFetcher))),
        ];

        if (flareSolverrConfigured)
            fetchers.Add(new ReweePriceFetcher(
                httpClientFactory.CreateClient(nameof(ReweePriceFetcher)),
                new FlareSolverrClient(httpClientFactory.CreateClient("FlareSolverr"))));

        return [.. fetchers];
    }

    internal static Dictionary<string, IChainPriceFetcher> AllByBrand(
        IHttpClientFactory httpClientFactory, bool flareSolverrConfigured) =>
        All(httpClientFactory, flareSolverrConfigured).ToDictionary(f => f.Brand);
}
