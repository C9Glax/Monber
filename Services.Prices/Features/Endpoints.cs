namespace Services.Prices.Features;

internal static class Endpoints
{
    internal static void MapEndpoints(this RouteGroupBuilder builder)
    {
        builder.MapGet("/stores", GetStoresEndpoint.Handle);
        builder.MapPost("/stores/update", PostUpdateStoresEndpoint.Handle);

        builder.MapGet("/prices", GetPricesByLocationEndpoint.Handle);
        builder.MapGet("/prices/store", GetPricesByStoreEndpoint.Handle);
        builder.MapGet("/prices/history", GetPriceHistoryEndpoint.Handle);
    }
}