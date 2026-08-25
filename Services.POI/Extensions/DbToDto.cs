using MonberAPI.PoiData.Database;
using Services.POI.Entities;

namespace Services.POI.Extensions;

internal static class DbToDto
{
    public static Store ToDto(this DbStore store) => new Store(
        store.Id,
        store.Name,
        store.Latitude,
        store.Longitude,
        store.Brand,
        store.OpeningHours
    );
}