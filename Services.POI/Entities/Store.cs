using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Services.POI.Entities;

[method: JsonConstructor]
public record Store(string? Name, double Latitude, double Longitude, string Brand, string? OpeningHours)
{
    [Description("The store brand")]
    public string Brand { get; init; } = Brand;
    
    [Description("The name of the store")]
    public string? Name { get; init; } = Name;
    
    [Description("The latitude of the store")]
    public double Latitude { get; init; } = Latitude;
    
    [Description("The longitude of the store")]
    public double Longitude { get; init; } = Longitude;
    
    [Description("The opening hours of the store")]
    public string? OpeningHours { get; init; } = OpeningHours;
}