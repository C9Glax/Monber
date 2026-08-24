using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Services.Prices.Entities;

[method: JsonConstructor]
public record StoreSummary(long StoreId, string Brand, string? Name, double? Latitude, double? Longitude)
{
    [Description("The id of the store, used as storeId in the /prices endpoints")]
    public long StoreId { get; init; } = StoreId;

    [Description("The store brand")]
    public string Brand { get; init; } = Brand;

    [Description("The name of the store")]
    public string? Name { get; init; } = Name;

    [Description("The latitude of the store, if known")]
    public double? Latitude { get; init; } = Latitude;

    [Description("The longitude of the store, if known")]
    public double? Longitude { get; init; } = Longitude;
}
