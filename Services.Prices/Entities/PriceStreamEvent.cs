using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Services.Prices.Entities;

[method: JsonConstructor]
public record PriceStreamEvent(long StoreId, bool HasPrices, PriceObservation[] Observations)
{
    [Description("The id of the store this event reports on")]
    public long StoreId { get; init; } = StoreId;

    [Description("Whether any price was found for this store - false if the store was checked but has no tracked prices")]
    public bool HasPrices { get; init; } = HasPrices;

    [Description("The prices observed for this store, empty when HasPrices is false")]
    public PriceObservation[] Observations { get; init; } = Observations;
}
