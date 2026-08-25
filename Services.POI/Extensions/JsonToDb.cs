using MonberAPI.PoiData.Database;

namespace Services.POI.Extensions;

internal static class JsonToDb
{
    internal static DbStore ToDbStore(this OverpassDataFetcher.Store store) => new DbStore(
        store.Id,
        store.StoreInfo.Name,
        store.Latitude,
        store.Longitude,
        store.StoreInfo.Brand,
        store.StoreInfo.Shop,
        store.StoreInfo.OpeningHours);
}