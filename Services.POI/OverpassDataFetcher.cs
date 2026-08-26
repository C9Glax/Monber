using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MonberAPI.PoiData.Database;
using Services.POI.Database;
using Services.POI.Extensions;

namespace Services.POI;

/// <summary>
/// Retrieves specific data from the overpass API
/// </summary>
internal static class OverpassDataFetcher
{
    private const int TimeoutSeconds = 120;
    private static readonly string[] StoreNames =
    {
        "Kaufland",
        "Rewe",
        "Netto",
        "HIT",
        "EDEKA",
        "Lidl",
        "ALDI",
        "Penny"
    };

    private static readonly HttpClient Client = new()
    {
        // HttpClient's own default (100s) is shorter than the query's own [timeout:120] budget,
        // so a genuinely slow-but-successful response could still get aborted client-side first.
        Timeout = TimeSpan.FromSeconds(TimeoutSeconds + 30),
        DefaultRequestHeaders =
        {
            UserAgent = { new("Monber", "0.1")}
        }
    };

    // "out center;" (not "out geom;"): for node elements Overpass still puts lat/lon at the top
    // level, but for way/relation elements - most mapped supermarkets, since many are mapped as
    // building outlines rather than a single point - "out geom;" only gives a bounds/geometry
    // array, no top-level lat/lon, which silently produced Latitude=0/Longitude=0 (missing
    // ~58% of matched stores, everything mapped as a way or relation). "center" adds a
    // {lat, lon} centroid for those instead - see Store.ResolvedLatitude/ResolvedLongitude.
    private const string BaseQuery = "data=[out:json][timeout:{0}];area(id:3600051477)->.searchArea;({1});out center;";

    private const string StoreQuery = """nwr["shop"]["brand"="{0}"](area.searchArea);""";

    private static string Query =>
        string.Format(BaseQuery,
            TimeoutSeconds,
            string.Join('\n', StoreNames.Select(store => string.Format(StoreQuery, store)))
        );

    private const string APIUrl = "https://maps.mail.ru/osm/tools/overpass/api/interpreter";

    private const string GetVersionQuery = "data=[out:json][timeout:2];out geom;";
    
    public static async Task<ResponseStruct?> GetOsmBaseTimestamp(CancellationToken ct)
    {
        HttpResponseMessage response = await Client.PostAsync(APIUrl, new StringContent(GetVersionQuery), ct);
        if(response is not { IsSuccessStatusCode: true }
           || await response.Content.ReadFromJsonAsync<ResponseStruct>(cancellationToken: ct) is not { } result)
            return null;
        return result;
    }

    public static async Task<ResponseStruct?> GetStores(CancellationToken ct)
    {
        StringContent content = new (Query);
        HttpResponseMessage response = await Client.PostAsync(APIUrl, content, ct);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<ResponseStruct>(ct);
    }

    internal static async Task LoadStores(Context context, CancellationToken ct)
    {
        if (await context.Version.AnyAsync(ct))
        {
            if (await context.Version.ToListAsync(ct) is not { Count: >0 } versions ||
                await GetOsmBaseTimestamp(ct) is not { Osm3S: { TimestampOsmBase: { } fetched } } ||
                fetched.Subtract(versions.Max(v => v.OsmBaseTimestamp)).TotalHours < 24)
            {
                return;
            }
        }

        if (await GetStores(ct) is not { Elements: { Length: >0 } stores, Osm3S: { TimestampOsmBase: { } timestamp }, Generator: { } generator })
        {
            return;
        }
        DbStore[] dbStores = [.. stores.Where(s => s.ResolvedLatitude != 0 && s.ResolvedLongitude != 0).Select(s => s.ToDbStore())];

        long[] storeIds = dbStores.Select(s => s.Id).ToArray();
        await context.Stores.Where(s => storeIds.All(id => id != s.Id)).ExecuteDeleteAsync(ct);
            
        long[] dbStoreIds = await context.Stores.Select(s => s.Id).ToArrayAsync(ct);
        DbStore[] newStores = [.. dbStores.ExceptBy(dbStoreIds, s => s.Id)];

        await context.Stores.AddRangeAsync(newStores, ct);
        await context.Version.AddAsync(new DbVersion(0, timestamp, generator), ct);
        await context.SaveChangesAsync(ct);
    }
        

    [method: JsonConstructor]
    internal record ResponseStruct(
        [property: JsonPropertyName("generator")] string Generator,
        [property: JsonPropertyName("osm3s")] OSM3SStruct Osm3S,
        [property: JsonPropertyName("elements")] Store[] Elements);

    [method: JsonConstructor]
    internal record OSM3SStruct(
        [property: JsonPropertyName("timestamp_osm_base")] DateTimeOffset TimestampOsmBase,
        [property: JsonPropertyName("copyright")] string Copyright);

    [method: JsonConstructor]
    internal record Store(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("lat")] double? Latitude,
        [property: JsonPropertyName("lon")] double? Longitude,
        [property: JsonPropertyName("center")] CenterStruct? Center,
        [property: JsonPropertyName("tags")] StoreInfo StoreInfo)
    {
        // Node elements have lat/lon directly; way/relation elements (most mapped supermarkets,
        // since many are mapped as building outlines) only have a "center" centroid instead - see
        // the "out center;" comment on BaseQuery above.
        internal double ResolvedLatitude => Latitude ?? Center?.Lat ?? 0;
        internal double ResolvedLongitude => Longitude ?? Center?.Lon ?? 0;
    }

    [method: JsonConstructor]
    internal record CenterStruct(
        [property: JsonPropertyName("lat")] double Lat,
        [property: JsonPropertyName("lon")] double Lon);

    [method: JsonConstructor]
    internal record StoreInfo(
        [property: JsonPropertyName("brand")] string Brand,
        [property: JsonPropertyName("shop")] string Shop,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("opening_hours")] string? OpeningHours);
}