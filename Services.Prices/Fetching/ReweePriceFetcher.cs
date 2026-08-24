using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Services.Prices.Fetching;

/// <summary>
/// REWE's stationary-store catalog (rewe.de) is regional: prices for a search only show once a market is
/// selected via a `wksMarketsCookie` cookie carrying that market's `wwIdent`, and the whole site sits
/// behind Cloudflare (see <see cref="FlareSolverrClient"/>). Store discovery instead reuses Overpass/OSM
/// (same area and technique as Services.POI) since REWE's own site only exposes a geo-search for nearby
/// markets, not a bulk "all stores" listing.
///
/// Nearest-market resolution: the site's market-chooser widget fetches `/api/frontend-includes` (POST,
/// `Content-Type: application/json`) with a small config array describing the widget to render - this is
/// the same shape captured from the site's own network traffic. The response's `content` is a server-
/// rendered HTML fragment listing nearby markets ordered by distance, each carrying its own `wwIdent` in an
/// inline hydration payload - so the first `wwIdent` found is the nearest market to the given coordinates.
///
/// Price lookup: fetching `/suche/uebersicht?searchTerm=...` with that market's `wksMarketsCookie` set
/// returns product tiles whose `aria-label` already embeds the price, e.g. `"Monster Energy Ultra White
/// 10x0,5l, 8,88 €"` - confirmed live by diffing the same search with and without the cookie set (no
/// cookie: price tag present but empty). Unlike the market-list POST, this particular path 403s a plain
/// HttpClient even with valid Cloudflare cookies replayed (confirmed live: same cookies, same request,
/// curl/HttpClient get 403 while a real browser doesn't - almost certainly TLS/JA3 fingerprinting), so this
/// one request per lookup goes through FlareSolverr itself rather than being replayed. REWE's catalog is
/// far more granular than the tracked-product list
/// (many sub-flavors, pack sizes, "Tiefpreis" labels), so a tracked product like "Monster Energy Ultra"
/// matches the first tile whose name - with the "Monster Energy" prefix and pack-size suffix stripped -
/// starts with the tracked flavor ("" for "Original", matching tiles with no extra flavor word at all).
/// </summary>
internal sealed partial class ReweePriceFetcher(HttpClient client, FlareSolverrClient flareSolverr) : IChainPriceFetcher
{
    public string Brand => "Rewe";

    private const string OverpassApiUrl = "https://maps.mail.ru/osm/tools/overpass/api/interpreter";
    private const string OverpassQuery =
        "data=[out:json][timeout:60];area(id:3600051477)->.searchArea;nwr[\"shop\"][\"brand\"=\"Rewe\"](area.searchArea);out geom;";

    private const string FrontendIncludesUrl = "https://www.rewe.de/api/frontend-includes";
    private const string SearchUrl = "https://www.rewe.de/suche/uebersicht?searchTerm=monster+energy";

    private string? _cookieHeader;
    private string? _userAgent;

    public async Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, OverpassApiUrl)
        {
            Content = new StringContent(OverpassQuery)
        };
        request.Headers.UserAgent.ParseAdd("Monber/0.1");

        HttpResponseMessage response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return [];

        OverpassResponse? result = await response.Content.ReadFromJsonAsync<OverpassResponse>(ct);
        if (result is not { Elements.Length: > 0 })
            return [];

        return [.. result.Elements
            .Where(e => e.Latitude != 0 && e.Longitude != 0)
            .Select(e => new ChainStore(e.Id.ToString(CultureInfo.InvariantCulture), e.Tags?.Name, e.Latitude, e.Longitude))];
    }

    public async Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct)
    {
        if (store.Latitude is not { } lat || store.Longitude is not { } lon)
            return [];

        string? wwIdent = await ResolveNearestMarketAsync(lat, lon, ct);
        if (wwIdent is null)
            return [];

        // The cookie value must be URL-encoded, same as the real site sets it - a raw JSON value contains
        // characters (", {, :) that aren't valid in a bare cookie value and get silently dropped.
        string marketCookieValue = Uri.EscapeDataString(
            $$$"""{"stationary":{"wwIdent":"{{{wwIdent}}}","serviceTypes":["STATIONARY"]}}""");
        FlareSolverrSolution? solution = await flareSolverr.GetAsync(
            SearchUrl, [new FlareSolverrCookie("wksMarketsCookie", marketCookieValue, ".rewe.de")], ct);
        string? html = solution?.Response;
        if (html is null)
            return [];

        (string Flavor, decimal Price)[] tiles = [.. ProductTileRegex().Matches(html)
            .Select(ParseTile)
            .Where(t => t is not null)
            .Select(t => t!.Value)];

        List<ChainPrice> results = [];
        foreach (string product in products)
        {
            string flavor = product.Replace("Monster Energy", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (flavor.Equals("Original", StringComparison.OrdinalIgnoreCase))
                flavor = ""; // REWE's plain-flavor tiles carry no flavor word at all.

            foreach ((string tileFlavor, decimal price) in tiles)
            {
                bool matches = flavor.Length == 0
                    ? tileFlavor.Length == 0
                    : tileFlavor.StartsWith(flavor, StringComparison.OrdinalIgnoreCase);
                if (!matches)
                    continue;

                results.Add(new ChainPrice(product, price, "EUR"));
                break;
            }
        }

        return [.. results];
    }

    private async Task<string?> ResolveNearestMarketAsync(double lat, double lon, CancellationToken ct)
    {
        string body = JsonSerializer.Serialize(new object[]
        {
            new
            {
                id = Guid.NewGuid().ToString(),
                name = "wks-market-list",
                @namespace = "market-chooser",
                query = new
                {
                    searchTerm = "",
                    page = "1",
                    longitude = lon,
                    latitude = lat,
                    productId = "",
                    hasUserInteracted = "false"
                }
            }
        });

        string? json = await SendWithClearanceAsync(() =>
        {
            HttpRequestMessage request = new(HttpMethod.Post, FrontendIncludesUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("Rd-Client-Href", "https://www.rewe.de/marktsuche");
            return request;
        }, ct);

        if (json is null)
            return null;

        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;

        string? html = doc.RootElement[0].TryGetProperty("content", out JsonElement contentEl)
            ? contentEl.GetString()
            : null;
        if (html is null)
            return null;

        Match match = WwIdentRegex().Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Sends a rewe.de request carrying the current Cloudflare clearance cookies/user agent, resolving them
    /// via FlareSolverr on first use and once more if the request comes back non-2xx (clearance likely
    /// expired).
    /// </summary>
    private async Task<string?> SendWithClearanceAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        if (_cookieHeader is null && !await RefreshClearanceAsync(ct))
            return null;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            using HttpRequestMessage request = requestFactory();
            request.Headers.TryAddWithoutValidation("Cookie", _cookieHeader!);
            if (_userAgent is not null)
                request.Headers.UserAgent.ParseAdd(_userAgent);

            HttpResponseMessage response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync(ct);

            if (attempt == 0 && !await RefreshClearanceAsync(ct))
                return null;
        }

        return null;
    }

    private async Task<bool> RefreshClearanceAsync(CancellationToken ct)
    {
        FlareSolverrSolution? solution = await flareSolverr.GetAsync("https://www.rewe.de/", null, ct);
        if (solution?.Cookies is not { Length: > 0 })
            return false;

        _cookieHeader = string.Join("; ", solution.Cookies.Select(c => $"{c.Name}={c.Value}"));
        _userAgent = solution.UserAgent;
        return true;
    }

    private static (string Flavor, decimal Price)? ParseTile(Match match)
    {
        if (!decimal.TryParse(
                match.Groups["price"].Value, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out decimal price))
            return null;

        string rest = match.Groups["name"].Value
            .Replace("Monster Energy", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        Match sizeMatch = SizeSuffixRegex().Match(rest);
        string flavor = sizeMatch.Success ? rest[..sizeMatch.Index].Trim() : rest;

        return (flavor, price);
    }

    [GeneratedRegex(@"aria-label=""(?<name>Monster Energy.+?),\s*(?:Tiefpreis\s+)?(?<price>\d+,\d+)\s*€""")]
    private static partial Regex ProductTileRegex();

    [GeneratedRegex(@"\d+(?:x\d+,\d+l|,\d+l)$")]
    private static partial Regex SizeSuffixRegex();

    [GeneratedRegex(@"""wwIdent""\s*:\s*""(\d+)""")]
    private static partial Regex WwIdentRegex();

    [method: JsonConstructor]
    private record OverpassResponse([property: JsonPropertyName("elements")] OverpassElement[] Elements);

    [method: JsonConstructor]
    private record OverpassElement(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("lat")] double Latitude,
        [property: JsonPropertyName("lon")] double Longitude,
        [property: JsonPropertyName("tags")] OverpassTags? Tags);

    [method: JsonConstructor]
    private record OverpassTags([property: JsonPropertyName("name")] string? Name);
}
