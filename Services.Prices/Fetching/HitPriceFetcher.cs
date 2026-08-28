using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Services.Prices.Fetching;

/// <summary>
/// hit.de has no bot protection (confirmed live: plain requests get clean 200s throughout) and embeds a
/// full store list directly on `/maerkte` as an HTML-entity-encoded `data-stores="[...]"` attribute -
/// confirmed live, ~90 stores with `url` (full store-page URL), `name`, and `location.{latitude,longitude}`.
///
/// Store selection: `GET {store.url}?mein-markt=1` sets `Set-Cookie: mein-markt={storeId}` (plain numeric
/// id, httponly) - confirmed live. With that cookie set, `/suche?suche=...` returns real per-store search
/// results; without it the page asks the visitor to pick a market first (same cookie-gating shape as
/// Rewe's `wksMarketsCookie`, just simpler - no JSON blob, no market-chooser round trip). Each result tile
/// carries a `data-leaflet="{...}"` JSON blob (HTML-entity-encoded, including numeric character
/// references like `&amp;#x7B;` for `{` - confirmed live, needs a full HTML decode not just `&amp;quot;`
/// replacement) with `storeId` (matches the selected store, confirming the price is genuinely
/// store-specific), `overview` (pack size, e.g. "0,5l Dose" - same trailing-suffix convention as
/// Rewe/Kaufland), and `priceTag.priceEuro`/`priceTag.priceCent` (the price split across two string
/// fields rather than one decimal field).
/// </summary>
internal sealed partial class HitPriceFetcher(HttpClient client) : IChainPriceFetcher
{
    public string Brand => "HIT";

    private const string StoresUrl = "https://www.hit.de/maerkte";
    private const string SearchUrlTemplate = "https://www.hit.de/suche?suche={0}";

    public async Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct)
    {
        string html = await client.GetStringAsync(StoresUrl, ct);
        Match match = DataStoresRegex().Match(html);
        if (!match.Success)
            return [];

        StoreDto[]? stores = JsonSerializer.Deserialize<StoreDto[]>(WebUtility.HtmlDecode(match.Groups[1].Value));
        if (stores is not { Length: > 0 })
            return [];

        return [.. stores
            .Where(s => s.Url is not null)
            .Select(s => new ChainStore(s.Url!, s.Name, s.Location?.Latitude, s.Location?.Longitude))];
    }

    public async Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct)
    {
        HttpResponseMessage selectResponse = await client.GetAsync($"{store.ExternalStoreId}?mein-markt=1", ct);
        if (!selectResponse.IsSuccessStatusCode)
            return [];

        string searchUrl = string.Format(SearchUrlTemplate, Uri.EscapeDataString("Monster Energy"));
        string html = await client.GetStringAsync(searchUrl, ct);

        (string PackSize, decimal Price)[] tiles = [.. DataLeafletRegex().Matches(html)
            .Select(m => JsonSerializer.Deserialize<LeafletDto>(WebUtility.HtmlDecode(m.Groups[1].Value)))
            .Select(ParseTile)
            .Where(t => t is not null)
            .Select(t => t!.Value)];

        List<ChainPrice> results = [];
        foreach (string product in products)
        {
            string packSize = product.Replace("Monster Energy", "", StringComparison.OrdinalIgnoreCase).Trim();

            foreach ((string tilePackSize, decimal price) in tiles)
            {
                if (!tilePackSize.Equals(packSize, StringComparison.OrdinalIgnoreCase))
                    continue;

                results.Add(new ChainPrice(product, price, "EUR", SourceUrl: searchUrl));
                break;
            }
        }

        return [.. results];
    }

    private static (string PackSize, decimal Price)? ParseTile(LeafletDto? leaflet)
    {
        if (leaflet?.Overview is not { } overview || leaflet.PriceTag is not { } priceTag)
            return null;

        Match sizeMatch = SizeSuffixRegex().Match(overview);
        if (!sizeMatch.Success)
            return null;

        if (priceTag.PriceEuro is null || priceTag.PriceCent is null)
            return null;
        if (!decimal.TryParse(
                $"{priceTag.PriceEuro}.{priceTag.PriceCent}", NumberStyles.Number, CultureInfo.InvariantCulture, out decimal price))
            return null;

        return (sizeMatch.Groups[1].Value, price);
    }

    [GeneratedRegex(@"data-stores=""(.+?)""", RegexOptions.Singleline)]
    private static partial Regex DataStoresRegex();

    [GeneratedRegex(@"data-leaflet=""(.+?)""", RegexOptions.Singleline)]
    private static partial Regex DataLeafletRegex();

    [GeneratedRegex(@"(\d+(?:x\d+,\d+l|,\d+l))\s+Dose$")]
    private static partial Regex SizeSuffixRegex();

    [method: JsonConstructor]
    private record StoreDto(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("location")] StoreLocationDto? Location);

    [method: JsonConstructor]
    private record StoreLocationDto(
        [property: JsonPropertyName("latitude")] double? Latitude,
        [property: JsonPropertyName("longitude")] double? Longitude);

    [method: JsonConstructor]
    private record LeafletDto(
        [property: JsonPropertyName("overview")] string? Overview,
        [property: JsonPropertyName("priceTag")] PriceTagDto? PriceTag);

    [method: JsonConstructor]
    private record PriceTagDto(
        [property: JsonPropertyName("priceEuro")] string? PriceEuro,
        [property: JsonPropertyName("priceCent")] string? PriceCent);
}
