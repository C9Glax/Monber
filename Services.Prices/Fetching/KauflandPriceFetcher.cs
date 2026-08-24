using System.Text.Json.Serialization;

namespace Services.Prices.Fetching;

/// <summary>
/// Proof-of-concept adapter: Kaufland's online shop (shop.kaufland.de) exposes JSON endpoints for its
/// market locator and product search, scoped to a chosen pickup market. The exact endpoint paths and
/// response shapes here are best-effort based on the shop's known structure and MUST be confirmed against
/// the live site (e.g. via browser devtools network capture) before relying on this in production - they
/// are not verifiable from this repository alone.
/// </summary>
internal class KauflandPriceFetcher(HttpClient client) : IChainPriceFetcher
{
    public string Brand => "Kaufland";

    private const string MarketsUrl = "https://filiale.kaufland.de/api/markets";
    private const string SearchUrlTemplate = "https://shop.kaufland.de/api/search?marketId={0}&q={1}";

    public async Task<ChainStorePrice[]> FetchAsync(string[] products, CancellationToken ct)
    {
        MarketDto[]? markets = await client.GetFromJsonAsync<MarketDto[]>(MarketsUrl, ct);
        if (markets is not { Length: > 0 })
            return [];

        List<ChainStorePrice> results = [];
        foreach (MarketDto market in markets)
        {
            foreach (string product in products)
            {
                string url = string.Format(SearchUrlTemplate, market.Id, Uri.EscapeDataString(product));
                SearchResponseDto? response = await client.GetFromJsonAsync<SearchResponseDto>(url, ct);
                if (response?.Results is not { Length: > 0 } items)
                    continue;

                SearchResultDto match = items[0];
                results.Add(new ChainStorePrice(
                    market.Id,
                    market.Name,
                    market.Latitude,
                    market.Longitude,
                    product,
                    match.Price,
                    match.Currency));
            }
        }

        return [.. results];
    }

    [method: JsonConstructor]
    private record MarketDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("lat")] double? Latitude,
        [property: JsonPropertyName("lon")] double? Longitude);

    [method: JsonConstructor]
    private record SearchResponseDto(
        [property: JsonPropertyName("results")] SearchResultDto[] Results);

    [method: JsonConstructor]
    private record SearchResultDto(
        [property: JsonPropertyName("price")] decimal Price,
        [property: JsonPropertyName("currency")] string Currency);
}
