using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Services.Prices.Fetching;

/// <summary>
/// Germany is split between two entirely separate ALDI companies with separate sites/backends - aldi-nord.de
/// (this fetcher) and aldi-sued.de (see AldiSuedPriceFetcher) - confirmed live: different hosting (Nord:
/// Azure Front Door/Next.js, Süd: Akamai-fronted Nuxt), no shared store/pricing data. OSM tags them as
/// distinct brands "Aldi Nord"/"Aldi Süd", not a combined "ALDI" (confirmed live via Overpass: brand="ALDI"
/// matches 0 stores, while "Aldi Nord"/"Aldi Süd" together match ~2,969) - Services.POI's brand list was
/// fixed to match (see OverpassDataFetcher.StoreNames), so each ALDI company gets its own IChainPriceFetcher
/// with its own Brand, the same as any other chain, rather than one fetcher guessing which company a store
/// belongs to.
///
/// Store discovery: aldi-nord.de/filialen-und-oeffnungszeiten.html has no bot protection (confirmed live)
/// and embeds a bulk store list (~2,234 stores) in its Next.js `__NEXT_DATA__` script, at
/// `props.pageProps.apiData` - itself a JSON-encoded string (not a nested object) containing an array of
/// `[requestKey, {req,res}]` pairs; the `"STORE_UBERALL_LOCATIONS_GET"` entry's `res.response.locations` is
/// the store list, each with `identifier`, `name`, `lat`, `lng` (all confirmed live).
///
/// Price lookup: aldi-nord.de/angebote.html uses the same `__NEXT_DATA__`/`apiData` double-encoding, this
/// time with an `"OFFER_GET"` entry whose `res.algoliaDataMap` is a flat map keyed by `objectID` of every
/// current flyer item (confirmed live) - pricing is nationwide (no store selector on this page at all), so
/// FetchPricesAsync's `store` parameter is unused beyond it needing to already be a known Aldi Nord store.
/// Each entry's `salesUnit` (e.g. "0,5-L-Dose", presumably "4x0,5-L-Dose" for multipacks though none were
/// observed live) is the pack-size signal, normalized to the same "0,5l"/"4x0,5l" suffix convention
/// Rewe/Kaufland/HIT use. `promotionPrices[]` entries carry `validFrom`/`validUntil` unix timestamps,
/// classified into current/future the same way KauflandPriceFetcher splits its own dated offers.
/// </summary>
internal sealed partial class AldiNordPriceFetcher(HttpClient client) : IChainPriceFetcher
{
    public string Brand => "Aldi Nord";

    private const string StoresUrl = "https://www.aldi-nord.de/filialen-und-oeffnungszeiten.html";
    private const string OffersUrl = "https://www.aldi-nord.de/angebote.html";

    public async Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct)
    {
        string html = await client.GetStringAsync(StoresUrl, ct);
        using JsonDocument? apiData = ExtractApiData(html);
        if (apiData is null)
            return [];

        JsonElement? locations = FindApiDataEntry(apiData.RootElement, "STORE_UBERALL_LOCATIONS_GET")
            ?.GetProperty("res").GetProperty("response").GetProperty("locations");
        if (locations is not { } locationsEl)
            return [];

        List<ChainStore> stores = [];
        foreach (JsonElement location in locationsEl.EnumerateArray())
        {
            string? identifier = location.TryGetProperty("identifier", out JsonElement idEl) ? idEl.GetString() : null;
            if (identifier is null)
                continue;

            string? name = location.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
            double? lat = location.TryGetProperty("lat", out JsonElement latEl) ? latEl.GetDouble() : null;
            double? lng = location.TryGetProperty("lng", out JsonElement lngEl) ? lngEl.GetDouble() : null;

            stores.Add(new ChainStore(identifier, name, lat, lng));
        }

        return [.. stores];
    }

    public async Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct)
    {
        string html = await client.GetStringAsync(OffersUrl, ct);
        using JsonDocument? apiData = ExtractApiData(html);
        if (apiData is null)
            return [];

        JsonElement? algoliaDataMap = FindApiDataEntry(apiData.RootElement, "OFFER_GET")
            ?.GetProperty("res").GetProperty("algoliaDataMap");
        if (algoliaDataMap is not { } offersEl)
            return [];

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        List<(string PackSize, decimal Price, DateOnly? EffectiveFrom)> monsterOffers = [];
        foreach (JsonElement offer in offersEl.EnumerateObject().Select(p => p.Value))
        {
            string? brandName = offer.TryGetProperty("brandName", out JsonElement bnEl) ? bnEl.GetString() : null;
            if (brandName is null || !brandName.Contains("MONSTER", StringComparison.OrdinalIgnoreCase))
                continue;

            string? salesUnit = offer.TryGetProperty("salesUnit", out JsonElement suEl) ? suEl.GetString() : null;
            string? packSize = salesUnit is null ? null : NormalizePackSize(salesUnit);
            if (packSize is null || !offer.TryGetProperty("promotionPrices", out JsonElement pricesEl))
                continue;

            foreach (JsonElement price in pricesEl.EnumerateArray())
            {
                if (!price.TryGetProperty("priceValue", out JsonElement pvEl) || !pvEl.TryGetDecimal(out decimal priceValue))
                    continue;

                DateOnly? validFrom = price.TryGetProperty("validFrom", out JsonElement vfEl) && vfEl.TryGetInt64(out long vf)
                    ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(vf).UtcDateTime)
                    : null;
                DateOnly? validUntil = price.TryGetProperty("validUntil", out JsonElement vuEl) && vuEl.TryGetInt64(out long vu)
                    ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(vu).UtcDateTime)
                    : null;

                if (validUntil is { } end && end < today)
                    continue; // Fully expired.

                DateOnly? effectiveFrom = validFrom is { } start && start > today ? start : null;
                monsterOffers.Add((packSize, priceValue, effectiveFrom));
            }
        }

        List<ChainPrice> results = [];
        foreach (string product in products)
        {
            string? productPackSize = PackSizeSuffixRegex().Match(product) is { Success: true } m ? m.Value : null;
            if (productPackSize is null)
                continue;

            bool foundCurrent = false, foundFuture = false;
            foreach ((string packSize, decimal price, DateOnly? effectiveFrom) in monsterOffers)
            {
                if (foundCurrent && foundFuture)
                    break;
                if (!packSize.Equals(productPackSize, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (effectiveFrom is null)
                {
                    if (foundCurrent)
                        continue;
                    results.Add(new ChainPrice(product, price, "EUR", SourceUrl: OffersUrl));
                    foundCurrent = true;
                }
                else
                {
                    if (foundFuture)
                        continue;
                    results.Add(new ChainPrice(product, price, "EUR", effectiveFrom, OffersUrl));
                    foundFuture = true;
                }
            }
        }

        return [.. results];
    }

    /// <summary>
    /// `props.pageProps.apiData` is a JSON string (double-encoded), not a nested object - it must be
    /// re-parsed as its own JSON document once extracted.
    /// </summary>
    private static JsonDocument? ExtractApiData(string html)
    {
        Match match = NextDataRegex().Match(html);
        if (!match.Success)
            return null;

        using JsonDocument outer = JsonDocument.Parse(match.Groups[1].Value);
        if (!outer.RootElement.TryGetProperty("props", out JsonElement props) ||
            !props.TryGetProperty("pageProps", out JsonElement pageProps) ||
            !pageProps.TryGetProperty("apiData", out JsonElement apiDataEl) ||
            apiDataEl.GetString() is not { } apiDataJson)
            return null;

        return JsonDocument.Parse(apiDataJson);
    }

    private static JsonElement? FindApiDataEntry(JsonElement apiData, string requestKey)
    {
        foreach (JsonElement pair in apiData.EnumerateArray())
        {
            if (pair.GetArrayLength() == 2 && pair[0].GetString() == requestKey)
                return pair[1];
        }

        return null;
    }

    /// <summary>Normalizes ALDI's "0,5-L-Dose"/"4x0,5-L-Dose" style unit into the "0,5l"/"4x0,5l" suffix
    /// convention Rewe/Kaufland/HIT use for tracked-product matching.</summary>
    private static string? NormalizePackSize(string salesUnit)
    {
        Match match = SalesUnitRegex().Match(salesUnit);
        if (!match.Success)
            return null;

        return match.Groups["qty"].Success
            ? $"{match.Groups["qty"].Value}x{match.Groups["vol"].Value}l"
            : $"{match.Groups["vol"].Value}l";
    }

    [GeneratedRegex(@"<script id=""__NEXT_DATA__"" type=""application/json"">(.+?)</script>", RegexOptions.Singleline)]
    private static partial Regex NextDataRegex();

    [GeneratedRegex(@"^(?:(?<qty>\d+)x)?(?<vol>\d+,\d+)-L-Dose$", RegexOptions.IgnoreCase)]
    private static partial Regex SalesUnitRegex();

    [GeneratedRegex(@"\d+(?:x\d+,\d+l|,\d+l)$")]
    private static partial Regex PackSizeSuffixRegex();
}
