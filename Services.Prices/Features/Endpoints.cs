namespace Services.Prices.Features;

internal static class Endpoints
{
    internal static void MapEndpoints(this RouteGroupBuilder builder)
    {
        builder.MapGet("/stores", GetStoresEndpoint.Handle);

        builder.MapGet("/prices", GetPricesEndpoint.Handle);
        builder.MapGet("/prices/history", GetPriceHistoryEndpoint.Handle);
        builder.MapPost("/prices/update", PostUpdatePricesEndpoint.Handle);
    }
}