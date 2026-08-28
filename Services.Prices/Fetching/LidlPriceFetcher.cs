using System.Globalization;
using System.Text.Json.Serialization;

namespace Services.Prices.Fetching;

/// <summary>
/// lidl.de/filialsuche is a static HTML directory of hardcoded city links, not a bulk store-locator API
/// (confirmed live) - unlike Kaufland's `.klstorefinder.json`, there's no single request that returns
/// every store. Store discovery therefore reuses Overpass/OSM instead, the same technique
/// ReweePriceFetcher uses for the same reason.
///
/// Lidl Germany unified pricing across all regional companies in October 2025 (confirmed via multiple
/// independent German outlets), so unlike Rewe/Kaufland/HIT there is no per-store price to look up - every
/// store gets the same national price. That would make FetchPricesAsync simpler than the other fetchers
/// (a single national lookup, `store` unused), but the actual online source for that national price isn't
/// reliable enough to build on yet: the weekly flyer (/c/aktuelle-angebote/...) is image-only (scanned-page
/// PNGs via imgproxy.leaflets.schwarz, no embedded structured data - confirmed live), and the webshop
/// search (lidl.de/q/search) returns hits but a specific Monster Energy product URL found via web search
/// 404'd live, suggesting webshop listings are transient online-only "Aktionsartikel" promos rather than a
/// stable mirror of in-store shelf stock/pricing - not confirmed reliable enough to cover all three
/// tracked pack sizes. FetchPricesAsync is therefore left unimplemented until a stable structured price
/// source is found; a future implementation should follow the same trailing-pack-size-suffix matching
/// convention as Rewe/Kaufland/HIT once one is.
/// </summary>
internal sealed class LidlPriceFetcher(HttpClient client) : IChainPriceFetcher
{
    public string Brand => "Lidl";

    private const string OverpassApiUrl = "https://maps.mail.ru/osm/tools/overpass/api/interpreter";
    private const string OverpassQuery =
        "data=[out:json][timeout:60];area(id:3600051477)->.searchArea;nwr[\"shop\"][\"brand\"=\"Lidl\"](area.searchArea);out geom;";

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

    public Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct) =>
        Task.FromResult<ChainPrice[]>([]); // No stable structured price source found yet - see class doc-comment.

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
