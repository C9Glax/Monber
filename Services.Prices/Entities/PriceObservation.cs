using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Services.Prices.Entities;

[method: JsonConstructor]
public record PriceObservation(long StoreId, string Brand, string? StoreName, string Product, decimal Price, string Currency, DateTimeOffset FetchedAt, DateOnly? EffectiveFrom = null)
{
    [Description("The id of the store this price was observed at")]
    public long StoreId { get; init; } = StoreId;

    [Description("The store brand")]
    public string Brand { get; init; } = Brand;

    [Description("The name of the store")]
    public string? StoreName { get; init; } = StoreName;

    [Description("The tracked product name")]
    public string Product { get; init; } = Product;

    [Description("The observed price")]
    public decimal Price { get; init; } = Price;

    [Description("The currency of the price")]
    public string Currency { get; init; } = Currency;

    [Description("When this price was observed")]
    public DateTimeOffset FetchedAt { get; init; } = FetchedAt;

    [Description("If set, this price is not yet active - it becomes effective on this date (e.g. an upcoming sale). Null means the price reflects the store as currently observed.")]
    public DateOnly? EffectiveFrom { get; init; } = EffectiveFrom;
}
