namespace Services.POI.Features;

internal static class Endpoints
{
    internal static void MapEndpoints(this RouteGroupBuilder builder)
    {
        builder.MapGet("/stores", GetStoresEndpoint.Handle);
        
        builder.MapPost("/stores/update", PostUpdateStoresEndpoint.Handle);
    }
}