using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Services.Prices.Fetching;

/// <summary>
/// Kaufland's filiale site (filiale.kaufland.de) exposes a store locator returning every German store in
/// one request, and a search page whose HTML embeds this week's promotional-offer data as JSON. There is
/// no full everyday-catalog price API - only the current flyer (Angebote).
///
/// KNOWN LIMITATION (confirmed live, not a guess): the `/.klstorebygeo.storeName={value}.json` call below
/// does NOT actually select the named store - live testing shows it ignores the storeName value entirely
/// and returns whatever store the server's IP-geolocation resolves the caller to (ignoring the value did
/// not even error; a bogus storeName still returned "Kaufland Neckarsulm", the chain's HQ location, or a
/// different geo-guessed store from a different network - never the requested one). No `Set-Cookie` was
/// observed either, so there's no session-selection happening. Net effect: FetchPricesAsync currently
/// returns real, live-fetched Kaufland flyer prices, but NOT reliably scoped to the requested `store` -
/// it reflects whichever store Kaufland's own geolocation picks for this server's outbound IP. Getting
/// real per-store scoping requires further investigation (e.g. a different selection mechanism, a
/// required first-visit/session-establishing request, or an entirely different endpoint) that a fresh
/// DevTools capture of an actual store switch on the live site would be needed to pin down.
/// </summary>
internal partial class KauflandPriceFetcher(HttpClient client) : IChainPriceFetcher
{
    public string Brand => "Kaufland";

    private const string StoreFinderUrl = "https://filiale.kaufland.de/.klstorefinder.json";
    private const string SelectStoreUrlTemplate = "https://filiale.kaufland.de/.klstorebygeo.storeName={0}.json";
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
        string selectUrl = string.Format(SelectStoreUrlTemplate, Uri.EscapeDataString(store.ExternalStoreId));
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
