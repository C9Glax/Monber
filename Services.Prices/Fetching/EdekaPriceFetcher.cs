using System.Globalization;
using System.Text.Json.Serialization;

namespace Services.Prices.Fetching;

/// <summary>
/// EDEKA has no accessible bulk store API - the only one (b2c-gw.api.edeka, documented at
/// b2c-gw.api.edeka/documentation/api/openapi.json) requires an OAuth2 client-credentials token issued to
/// registered B2B partners (confirmed live: GET /v3/markets with no token -&gt; 403), and www.edeka.de
/// itself sits behind Akamai bot protection (confirmed live: `server: AkamaiGHost`, 403 on
/// /marktsuche.jsp, /angebote/, and /api/market-gateway* even with a real browser user agent). Store
/// discovery therefore reuses Overpass/OSM instead, the same technique ReweePriceFetcher uses for the
/// same reason.
///
/// EDEKA is a franchise cooperative of ~11,000 independently owned stores with no central in-store price
/// feed - unlike Rewe (single site, per-store cookie) or Kaufland (single flyer feed, per-store geo-
/// cookie), there is no chain-wide source of truth for what an individual EDEKA store charges. The one
/// lead found, shop.edeka (a separate, Akamai-free, third-party "maexware-kundencloud" delivery/click-
/// and-collect platform), only covers the subset of stores that opted into delivery and prices orders
/// rather than in-store shelf stock, so it isn't representative enough to build on. FetchPricesAsync is
/// therefore left unimplemented until a viable per-store price source turns up.
/// </summary>
internal sealed class EdekaPriceFetcher(HttpClient client) : IChainPriceFetcher
{
    public string Brand => "EDEKA";

    private const string OverpassApiUrl = "https://maps.mail.ru/osm/tools/overpass/api/interpreter";
    private const string OverpassQuery =
        "data=[out:json][timeout:60];area(id:3600051477)->.searchArea;nwr[\"shop\"][\"brand\"=\"EDEKA\"](area.searchArea);out geom;";

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
        Task.FromResult<ChainPrice[]>([]); // No viable per-store price source found - see class doc-comment.

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
