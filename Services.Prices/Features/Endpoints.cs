using Microsoft.AspNetCore.Http;
using Services.Prices.Entities;

namespace Services.Prices.Features;

internal static class Endpoints
{
    internal static void MapEndpoints(this RouteGroupBuilder builder)
    {
        builder.MapGet("/stores", GetStoresEndpoint.Handle);
        builder.MapPost("/stores/update", PostUpdateStoresEndpoint.Handle);

        // GetPricesByLocationEndpoint writes NDJSON straight to the response body rather than returning
        // a typed result, so it has no return type for AddOpenApi to infer a schema from - Produces<>
        // documents the shape of each streamed line (see PriceStreamEvent) so the generated OpenAPI
        // document (and the frontend's TypeScript types generated from it) still describe it.
        builder.MapGet("/prices", GetPricesByLocationEndpoint.Handle)
            .Produces<PriceStreamEvent>(StatusCodes.Status200OK, "application/x-ndjson");
        builder.MapGet("/prices/store", GetPricesByStoreEndpoint.Handle);
        builder.MapPost("/prices/store/refresh", PostRefreshStorePricesEndpoint.Handle);
        builder.MapGet("/prices/history", GetPriceHistoryEndpoint.Handle);
    }
}