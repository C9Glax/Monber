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

            // Kaufland's search endpoint ignores the query string entirely - confirmed live: searching for
            // each of the three tracked pack sizes returns the same small fixed set of generic offers
            // (whatever loosely matches "Monster" that week: toys, batteries, and at most one real
            // "MONSTER / Energy Drink" entry), just reordered. That one real entry's `unit` field (e.g.
            // "je 0,5-l-Dose") is the only place the pack size it actually covers is stated - Kaufland's
            // flyer has so far never carried a multi-pack (4x/10x) Monster offer, only single 0.5 L cans.
            // So a tracked product only gets a price when its own pack-size suffix (the trailing
            // "0,5l"/"4x0,5l"-style token, same convention ReweePriceFetcher uses) matches the offer's
            // `unit`-derived pack size exactly - matching every product to whichever generic offer merely
            // contains "Monster" would (and did) misattribute the single-can price to the 4-pack/10-pack.
            //
            // The same search response also mixes in next week's flyer once it's published - a
            // dateFrom in the future - so each matching offer is dated instead of just taking the first
            // hit: an offer already covering today is a current price, one starting later is a future
            // price (see ChainPrice.EffectiveFrom), and anything already expired is ignored.
            string? productPackSize = PackSizeSuffixRegex().Match(product) is { Success: true } productSizeMatch
                ? productSizeMatch.Value
                : null;
            if (productPackSize is null)
                continue;

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            bool foundCurrent = false, foundFuture = false;
            foreach (JsonElement offer in offerData.EnumerateArray())
            {
                if (foundCurrent && foundFuture)
                    break;

                string? title = offer.TryGetProperty("title", out JsonElement t) ? t.GetString() : null;
                string? subtitle = offer.TryGetProperty("subtitle", out JsonElement st) ? st.GetString() : null;
                if (title is not "MONSTER" || subtitle is null ||
                    !subtitle.Contains("Energy Drink", StringComparison.OrdinalIgnoreCase))
                    continue;

                string? unit = offer.TryGetProperty("unit", out JsonElement u) ? u.GetString() : null;
                Match unitSizeMatch = unit is null ? Match.Empty : UnitPackSizeRegex().Match(unit);
                if (!unitSizeMatch.Success)
                    continue;
                string offerPackSize = unitSizeMatch.Groups["qty"].Success
                    ? $"{unitSizeMatch.Groups["qty"].Value}x{unitSizeMatch.Groups["vol"].Value}l"
                    : $"{unitSizeMatch.Groups["vol"].Value}l";
                if (!offerPackSize.Equals(productPackSize, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!offer.TryGetProperty("price", out JsonElement priceEl) || !priceEl.TryGetDecimal(out decimal price))
                    continue;

                DateOnly? dateFrom = offer.TryGetProperty("dateFrom", out JsonElement fromEl) &&
                                      DateOnly.TryParse(fromEl.GetString(), out DateOnly from)
                    ? from
                    : null;
                DateOnly? dateTo = offer.TryGetProperty("dateTo", out JsonElement toEl) &&
                                    DateOnly.TryParse(toEl.GetString(), out DateOnly to)
                    ? to
                    : null;

                if (dateTo is { } end && end < today)
                    continue; // Fully expired offer.

                if (dateFrom is { } start && start > today)
                {
                    if (foundFuture)
                        continue;
                    results.Add(new ChainPrice(product, price, "EUR", start, searchUrl));
                    foundFuture = true;
                }
                else
                {
                    if (foundCurrent)
                        continue;
                    results.Add(new ChainPrice(product, price, "EUR", SourceUrl: searchUrl));
                    foundCurrent = true;
                }
            }
        }

        return [.. results];
    }

    [GeneratedRegex(@"window\.SSR\['[^']+'\]\s*=\s*(\{.*?\})\s*;?\s*</script>", RegexOptions.Singleline)]
    private static partial Regex SsrBlobRegex();

    [GeneratedRegex(@"\d+(?:x\d+,\d+l|,\d+l)$")]
    private static partial Regex PackSizeSuffixRegex();

    [GeneratedRegex(@"(?:(?<qty>\d+)x)?(?<vol>\d+,\d+)-l")]
    private static partial Regex UnitPackSizeRegex();

    [method: JsonConstructor]
    private record StoreDto(
        [property: JsonPropertyName("n")] string Id,
        [property: JsonPropertyName("cn")] string? Name,
        [property: JsonPropertyName("lat")] string? Latitude,
        [property: JsonPropertyName("lng")] string? Longitude,
        [property: JsonPropertyName("friendlyUrl")] string? FriendlyUrl);
}
