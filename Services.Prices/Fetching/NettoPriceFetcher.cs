using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Services.Prices.Fetching;

/// <summary>
/// "Netto" here is the Scottie-dog chain (netto.de, owned by Denmark's Salling Group) - NOT "Netto
/// Marken-Discount" (netto-online.de, Edeka-owned, no dog). Confirmed live: Services.POI's Overpass query
/// filters `brand="Netto"` exactly, and stores matching that filter carry `brand:wikidata=Q552652` /
/// `operator="Netto ApS &amp; Co. KG"` / `contact:website=https://www.netto.de/...` - i.e. the Salling
/// chain. netto.de's client bundle calls Salling's white-label "Tjek" digital-flyer platform
/// (squid-api.tjek.com, dealer id `90f2VL` for Netto DE) for both stores and offers, unauthenticated -
/// confirmed live. netto.de itself is a plain Next.js/Vercel app with no Cloudflare/bot protection, so
/// this fetcher doesn't need FlareSolverr.
///
/// Offers are dealer-wide, not per-store (`store_id` is null on every offer) - confirmed live: Netto,
/// unlike Kaufland, has one national flyer, so FetchPricesAsync's `store` parameter only matters insofar
/// as the store must already be a known Netto store; the actual lookup ignores its id/coordinates. The
/// `query` filter parameter on /v2/offers is ignored server-side (confirmed live, same finding as
/// Kaufland's search) so offers are paged through and filtered client-side instead, matched by pack size
/// using the same trailing-suffix convention as Rewe/Kaufland, and split into current vs future-dated
/// prices the same way KauflandPriceFetcher does.
/// </summary>
internal sealed partial class NettoPriceFetcher(HttpClient client) : IChainPriceFetcher
{
    public string Brand => "Netto";

    private const string DealerId = "90f2VL";
    private const string StoresUrlTemplate = "https://squid-api.tjek.com/v2/stores?dealer_ids={0}&offset={1}";
    private const string OffersUrlTemplate = "https://squid-api.tjek.com/v2/offers?dealer_ids={0}&offset={1}";
    private const int PageSize = 24;

    public async Task<ChainStore[]> DiscoverStoresAsync(CancellationToken ct)
    {
        List<ChainStore> stores = [];
        for (int offset = 0; ; offset += PageSize)
        {
            StoreDto[]? page = await client.GetFromJsonAsync<StoreDto[]>(
                string.Format(StoresUrlTemplate, DealerId, offset), ct);
            if (page is not { Length: > 0 })
                break;

            stores.AddRange(page.Select(s => new ChainStore(s.Id, s.Name, s.Latitude, s.Longitude)));

            if (page.Length < PageSize)
                break;
        }

        return [.. stores];
    }

    public async Task<ChainPrice[]> FetchPricesAsync(ChainStore store, string[] products, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        List<(string PackSize, decimal Price, DateOnly? EffectiveFrom, string SourceUrl)> monsterOffers = [];

        for (int offset = 0; ; offset += PageSize)
        {
            string offersUrl = string.Format(OffersUrlTemplate, DealerId, offset);
            OfferDto[]? page = await client.GetFromJsonAsync<OfferDto[]>(offersUrl, ct);
            if (page is not { Length: > 0 })
                break;

            foreach (OfferDto offer in page)
            {
                if (offer.Heading is null ||
                    !offer.Heading.Contains("Monster", StringComparison.OrdinalIgnoreCase) ||
                    !offer.Heading.Contains("Energy", StringComparison.OrdinalIgnoreCase))
                    continue;

                string? packSize = ResolvePackSize(offer);
                if (packSize is null || offer.Pricing?.Price is not { } price)
                    continue;

                DateOnly? runTill = ParseDate(offer.RunTill);
                if (runTill is { } end && end < today)
                    continue; // Fully expired offer.

                DateOnly? runFrom = ParseDate(offer.RunFrom);
                DateOnly? effectiveFrom = runFrom is { } start && start > today ? start : null;

                monsterOffers.Add((packSize, price, effectiveFrom, offersUrl));
            }

            if (page.Length < PageSize)
                break;
        }

        List<ChainPrice> results = [];
        foreach (string product in products)
        {
            string? productPackSize = PackSizeSuffixRegex().Match(product) is { Success: true } m ? m.Value : null;
            if (productPackSize is null)
                continue;

            bool foundCurrent = false, foundFuture = false;
            foreach ((string packSize, decimal price, DateOnly? effectiveFrom, string sourceUrl) in monsterOffers)
            {
                if (foundCurrent && foundFuture)
                    break;
                if (!packSize.Equals(productPackSize, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (effectiveFrom is null)
                {
                    if (foundCurrent)
                        continue;
                    results.Add(new ChainPrice(product, price, "EUR", SourceUrl: sourceUrl));
                    foundCurrent = true;
                }
                else
                {
                    if (foundFuture)
                        continue;
                    results.Add(new ChainPrice(product, price, "EUR", effectiveFrom, sourceUrl));
                    foundFuture = true;
                }
            }
        }

        return [.. results];
    }

    /// <summary>
    /// Tjek's `quantity.size.from`/`to` + `unit.symbol` describe a single item's size (e.g. a 0.5 L can);
    /// `quantity.pieces.from`/`to` describes how many of that item are in the offer (e.g. 4 or 10 cans) -
    /// combined the same way Kaufland derives "4x0,5l" from its own `unit` string.
    /// </summary>
    private static string? ResolvePackSize(OfferDto offer)
    {
        QuantitySizeDto? size = offer.Quantity?.Size;
        string? unit = offer.Quantity?.Unit?.Symbol;
        if (size?.From is not { } volume || unit is null ||
            !unit.Equals("l", StringComparison.OrdinalIgnoreCase))
            return null;

        string volumeToken = volume.ToString("0.0##", CultureInfo.InvariantCulture).Replace('.', ',');

        int pieces = offer.Quantity?.Pieces?.From is { } p && p >= 1 ? (int)p : 1;
        return pieces > 1 ? $"{pieces}x{volumeToken}l" : $"{volumeToken}l";
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date) ? date : null;

    [GeneratedRegex(@"\d+(?:x\d+,\d+l|,\d+l)$")]
    private static partial Regex PackSizeSuffixRegex();

    [method: JsonConstructor]
    private record StoreDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("latitude")] double? Latitude,
        [property: JsonPropertyName("longitude")] double? Longitude);

    [method: JsonConstructor]
    private record OfferDto(
        [property: JsonPropertyName("heading")] string? Heading,
        [property: JsonPropertyName("pricing")] PricingDto? Pricing,
        [property: JsonPropertyName("quantity")] QuantityDto? Quantity,
        [property: JsonPropertyName("run_from")] string? RunFrom,
        [property: JsonPropertyName("run_till")] string? RunTill);

    [method: JsonConstructor]
    private record PricingDto([property: JsonPropertyName("price")] decimal? Price);

    [method: JsonConstructor]
    private record QuantityDto(
        [property: JsonPropertyName("size")] QuantitySizeDto? Size,
        [property: JsonPropertyName("unit")] QuantityUnitDto? Unit,
        [property: JsonPropertyName("pieces")] QuantitySizeDto? Pieces);

    [method: JsonConstructor]
    private record QuantitySizeDto([property: JsonPropertyName("from")] double? From);

    [method: JsonConstructor]
    private record QuantityUnitDto([property: JsonPropertyName("symbol")] string? Symbol);
}
