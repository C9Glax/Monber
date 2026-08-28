using System.Globalization;
using System.Text.Json.Serialization;

namespace Services.Prices.Fetching;

/// <summary>
/// See AldiNordPriceFetcher for the Nord/Süd split rationale. aldi-sued.de is a separate company/site
/// (Nuxt, Akamai-fronted) with no bulk store-locator endpoint found: its filialen page (unlike Nord's)
/// doesn't embed a full store list server-side, and its `asl.api.aldi-sued.de`/`api.aldi-sued.de` API
/// hosts (found in the page's Nuxt runtime config) both 404 on every guessed path without a captured real
/// browser network trace. Store discovery therefore reuses Overpass/OSM instead, filtered to the
/// "Aldi Süd" brand tag (same technique ReweePriceFetcher/EdekaPriceFetcher/LidlPriceFetcher use).
///
/// Price lookup: aldi-sued.de sits behind Akamai Bot Manager - confirmed live via FlareSolverr that it
/// *does* clear the challenge (200, full rendered HTML, for both the store finder and the offers page at
/// aldi-sued.de/angebote). But the offers page's `__NUXT__`/CMS payload turned out to be AEM-templated
/// (a JSON-LD `productJsonLdTemplate` with literal `"###price###"`-style placeholders, not the real
/// per-product data - confirmed live) - the actual current flyer prices are populated by further calls
/// into the `publish.prod.emea.cms.aldi.cx` content API that weren't reverse-engineered. FetchPricesAsync
/// is therefore left unimplemented; a future attempt should drive that content API (via FlareSolverr,
/// FlareSolverr:Url is already wired up for ReweePriceFetcher) rather than the rendered HTML.
/// </summary>
internal sealed class AldiSuedPriceFetcher(HttpClient client) : IChainPriceFetcher
{
    public string Brand => "Aldi Süd";

    private const string OverpassApiUrl = "https://maps.mail.ru/osm/tools/overpass/api/interpreter";
    private const string OverpassQuery =
        "data=[out:json][timeout:60];area(id:3600051477)->.searchArea;nwr[\"shop\"][\"brand\"=\"Aldi Süd\"](area.searchArea);out geom;";

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
        Task.FromResult<ChainPrice[]>([]); // Akamai clears via FlareSolverr, but no real price data found yet - see class doc-comment.

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
