using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Services.Prices.Fetching;

/// <summary>
/// Kaufland's filiale site (filiale.kaufland.de) exposes a store locator returning every German store in
/// one request, and a search page whose HTML embeds this week's promotional-offer data as JSON. There is
/// no full everyday-catalog price API - only the current flyer (Angebote).
///
/// Store selection: the `/.klstorebygeo.storeName={value}.json` selector (from the site's own JS
/// settings) turned out to be a dead end - live testing showed it ignores the storeName value and falls
/// back to IP-geolocation. But `/.klstorebygeo.json?lat={lat}&lng={lng}` (note: `lng`, not `lon`) DOES
/// work - confirmed live: it returns the exact store nearest those coordinates, sets a real `affinity`
/// session cookie, and a subsequent `/suche.html` search genuinely reflects that store's own flyer
/// (verified by selecting two different stores 700km apart and observing the store id in each response
/// match what was requested). Since DiscoverStoresAsync already captures each store's own lat/lng from
/// the store finder, we select stores by their own coordinates rather than any chain-provided identifier.
/// </summary>
internal partial class KauflandPriceFetcher(HttpClient client) : IChainPriceFetcher
{
    public string Brand => "Kaufland";

    private const string StoreFinderUrl = "https://filiale.kaufland.de/.klstorefinder.json";
    private const string SelectStoreUrlTemplate = "https://filiale.kaufland.de/.klstorebygeo.json?lat={0}&lng={1}";
    private const string SearchUrlTemplate = "https://filiale.kaufland.de/suche.html?q={0}";

    public async Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct)
    {
        StoreDto[]? stores = await client.GetFromJsonAsync<StoreDto[]>(StoreFinderUrl, ct);
        if (stores is not { Length: > 0 })
            return [];

        return [.. stores
            .Where(s => s.FriendlyUrl is not null)
            .Select(s => new ChainStore(
                s.FriendlyUrl!,
                s.Name,
                double.TryParse(s.Latitude, out double lat) ? lat : null,
                double.TryParse(s.Longitude, out double lon) ? lon : null))];
    }

    public async Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct)
    {
        if (store.Latitude is not { } lat || store.Longitude is not { } lng)
            return [];

        string selectUrl = string.Format(
            SelectStoreUrlTemplate,
            lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
            lng.ToString(System.Globalization.CultureInfo.InvariantCulture));
        HttpResponseMessage selectResponse = await client.GetAsync(selectUrl, ct);
        if (!selectResponse.IsSuccessStatusCode)
            return [];

        List<ChainPrice> results = [];
        foreach (string product in products)
        {
            string searchUrl = string.Format(SearchUrlTemplate, Uri.EscapeDataString(product));
            string html = await client.GetStringAsync(searchUrl, ct);

            Match match = SsrBlobRegex().Match(html);
            if (!match.Success)
                continue;

            using JsonDocument doc = JsonDocument.Parse(match.Groups[1].Value);
            if (!doc.RootElement.TryGetProperty("props", out JsonElement props) ||
                !props.TryGetProperty("offerData", out JsonElement offerData))
                continue;

            // Kaufland's weekly-offer search only ever returns a generic "MONSTER / Energy Drink" entry
            // (no flavor breakdown), so matching is brand-level: a hit means "the queried product's brand
            // is on this week's flyer for this store", not "this exact flavor is confirmed in stock".
            foreach (JsonElement offer in offerData.EnumerateArray())
            {
                string? title = offer.TryGetProperty("title", out JsonElement t) ? t.GetString() : null;
                if (title is null || !product.Contains(title, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (offer.TryGetProperty("price", out JsonElement priceEl) && priceEl.TryGetDecimal(out decimal price))
                {
                    results.Add(new ChainPrice(product, price, "EUR"));
                    break;
                }
            }
        }

        return [.. results];
    }

    [GeneratedRegex(@"window\.SSR\['[^']+'\]\s*=\s*(\{.*?\})\s*;?\s*</script>", RegexOptions.Singleline)]
    private static partial Regex SsrBlobRegex();

    [method: JsonConstructor]
    private record StoreDto(
        [property: JsonPropertyName("n")] string Id,
        [property: JsonPropertyName("cn")] string? Name,
        [property: JsonPropertyName("lat")] string? Latitude,
        [property: JsonPropertyName("lng")] string? Longitude,
        [property: JsonPropertyName("friendlyUrl")] string? FriendlyUrl);
}
