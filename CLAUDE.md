# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

MonberAPI is a .NET 10 / ASP.NET Core Aspire solution made up of two independent minimal-API microservices orchestrated by an Aspire AppHost:

- `Services.POI` — points-of-interest service. Stores German grocery store locations (Kaufland, Rewe, Netto, HIT, EDEKA, Lidl, Aldi Nord, Aldi Süd, Penny) fetched from the Overpass API (OpenStreetMap) and persisted in a local SQLite database (`stores.db`), queried by geographic radius.
- `Services.Prices` — a stub service with the endpoint-mapping scaffolding in place but no endpoints implemented yet.

Shared cross-cutting concerns (OpenTelemetry, health checks, service discovery, HTTP resilience) live in `MonberAPI.ServiceDefaults` and are wired into every service via `builder.AddServiceDefaults()`.

## Commands

Build the whole solution:
```
dotnet build MonberAPI.slnx
```

Run everything (both services + Aspire dashboard) via the AppHost:
```
dotnet run --project MonberAPI.AppHost
```

Run a single service directly (bypasses Aspire orchestration):
```
dotnet run --project Services.POI
dotnet run --project Services.Prices
```

Each service exposes Scalar API docs at `/docs` and OpenAPI at `/openapi/v1.json` when running.

There are no test projects in this solution currently.

### EF Core migrations (Services.POI)

The SQLite schema is managed via EF Core migrations in `Services.POI/Migrations`. Migrations run automatically on startup (`ctx.Database.MigrateAsync()` in `Program.cs`). To add a new migration after changing `Context`/entities:
```
dotnet ef migrations add <Name> --project Services.POI
```

## Architecture

### Solution layout
- `MonberAPI.AppHost` — Aspire orchestrator; `AppHost.cs` registers each service project (`services-poi`, `services-prices`) as a distributed application resource.
- `MonberAPI.ServiceDefaults` — shared `AddServiceDefaults()`/`MapDefaultEndpoints()` extensions (OpenTelemetry, health checks at `/health` and `/alive`, service discovery, resilient `HttpClient` defaults). Referenced by every service.
- `Services.POI`, `Services.Prices` — the actual services, each a `Microsoft.NET.Sdk.Web` minimal-API project.

### Per-service feature layout (vertical slice, not layered)

Each service maps its routes through a `Features/Endpoints.cs` with a `MapEndpoints(this RouteGroupBuilder)` extension called from `Program.cs` via `app.MapGroup("").MapEndpoints()`. Each route handler lives in its own static/abstract class file named `<Verb><Resource>Endpoint.cs` (e.g. `GetStoresEndpoint`, `PostUpdateStoresEndpoint`) exposing a single static `Handle` method used directly as the minimal-API delegate, with dependencies (DbContext, route/query params, `CancellationToken`) injected as parameters. Follow this pattern for new endpoints rather than introducing controllers or a service layer.

### Services.POI data flow

- `Entities/Store.cs` — public DTO returned by the API (has `[Description]` attributes for OpenAPI).
- `Database/DbStore.cs`, `Database/DbVersion.cs` — internal EF Core entities (`stores` and `versions` tables), configured in `Database/Context.cs`.
- `Extensions/DbToDto.cs` / `Extensions/JsonToDb.cs` — mapping extensions between the Overpass JSON model, the DB entity, and the public DTO. Keep this three-way separation (external JSON shape → DB entity → API DTO) when touching store data.
- `OverpassDataFetcher.cs` — queries the Overpass API (`https://maps.mail.ru/osm/tools/overpass/api/interpreter`) for the configured `StoreNames` brand list within a fixed OSM area, and syncs the `stores` table: deletes stores no longer present upstream, inserts new ones, and skips the whole sync if the last recorded `DbVersion.OsmBaseTimestamp` is less than 24 hours old (checked via a lightweight version-only query first). This sync runs once automatically on service startup (`Program.cs`) in addition to being reachable via `POST /stores/update`.
- `GetStoresEndpoint` (`GET /stores?lat=&lon=`) runs a raw-SQL haversine-distance query (`FromSql`) against `stores`, hardcoded to a 30 km radius, ordered by distance.

When modifying the Overpass query or store brand list, note the query is built via naive `string.Format` into a single POST body — there's no query builder abstraction.
