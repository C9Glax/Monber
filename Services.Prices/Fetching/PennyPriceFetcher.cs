using System.Globalization;
using System.Text.Json.Serialization;

namespace Services.Prices.Fetching;

/// <summary>
/// PENNY (part of REWE Group, like Rewe itself) exposes a bulk store list at `GET
/// https://www.penny.de/.rest/market` - a single unauthenticated JSON array of every German PENNY store
/// (confirmed live, ~2,100 stores), including `wwIdent` (the same field name REWE uses for its own market
/// identifier - confirming a shared REWE-Group backend), `marketName`, and string `latitude`/`longitude`.
/// This is strictly simpler than Rewe's own discovery (no Overpass/OSM dependency needed at all).
///
/// Price lookup is not implemented: PENNY doesn't mirror Rewe's `/suche` search page (confirmed live,
/// 404s), and its weekly-offers page (`/angebote`) does select a market via the same store-list widget but
/// renders its offer tiles as empty server-side placeholders - the real content is populated by a
/// same-origin XHR (the page's CSP `connect-src` only allows `www.penny.de`/`cdn.penny.de`) whose actual
/// URL wasn't found via static analysis (confirmed live: every guessed `/.rest/...` offers-style path
/// returned a Magnolia CMS login 401, which is inconclusive - could be a gated real endpoint or just an
/// unrelated CMS content node). Finding it would need a real browser network capture (e.g. via
/// FlareSolverr) rather than further guessing. FetchPricesAsync is therefore left unimplemented until that
/// endpoint is identified; once it is, matching should follow the same trailing-pack-size-suffix
/// convention as Rewe/Kaufland/HIT, and likely needs Kaufland-style current/future offer date handling
/// since PENNY's pricing is flyer-driven like Kaufland's.
/// </summary>
internal sealed class PennyPriceFetcher(HttpClient client) : IChainPriceFetcher
{
    public string Brand => "Penny";

    private const string StoresUrl = "https://www.penny.de/.rest/market";

    public async Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct)
    {
        StoreDto[]? stores = await client.GetFromJsonAsync<StoreDto[]>(StoresUrl, ct);
        if (stores is not { Length: > 0 })
            return [];

        return [.. stores
            .Where(s => s.WwIdent is not null)
            .Select(s => new ChainStore(
                s.WwIdent!,
                s.MarketName,
                double.TryParse(s.Latitude, NumberStyles.Number, CultureInfo.InvariantCulture, out double lat) ? lat : null,
                double.TryParse(s.Longitude, NumberStyles.Number, CultureInfo.InvariantCulture, out double lon) ? lon : null))];
    }

    public Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct) =>
        Task.FromResult<ChainPrice[]>([]); // No offers endpoint identified yet - see class doc-comment.

    [method: JsonConstructor]
    private record StoreDto(
        [property: JsonPropertyName("wwIdent")] string? WwIdent,
        [property: JsonPropertyName("marketName")] string? MarketName,
        [property: JsonPropertyName("latitude")] string? Latitude,
        [property: JsonPropertyName("longitude")] string? Longitude);
}
