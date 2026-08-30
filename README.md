# MonberAPI

Monber tracks German grocery prices by store location: it maps points-of-interest (grocery
stores, from OpenStreetMap) and the prices tracked at those stores (scraped from each chain's
website), and serves both through a single gateway to a Nuxt map frontend.

The solution is a .NET 10 / ASP.NET Core Aspire app made up of independent minimal-API
microservices, a reverse-proxy gateway, and a Nuxt frontend, orchestrated locally by an Aspire
AppHost and deployed as Docker Compose in production.

## Architecture

| Project | Role |
|---|---|
| `MonberAPI.AppHost` | Aspire orchestrator for local dev; also generates `deploy/docker-compose.yaml` via `aspire publish` |
| `MonberAPI.Gateway` | YARP reverse proxy — single external entry point, routes `/poi/*` → Services.POI, `/prices/*` → Services.Prices, everything else → the frontend |
| `MonberAPI.ServiceDefaults` | Shared OpenTelemetry, health checks (`/health`, `/alive`), service discovery, resilient `HttpClient` defaults — referenced by every service |
| `MonberAPI.PoiData` | Shared `stores` table entity (`DbStore`), referenced by both `Services.POI` (owner/writer) and `Services.Prices` (reader, for matching chain stores to locations) |
| `Services.POI` | Fetches German grocery store locations from the Overpass API (OpenStreetMap) for Kaufland, Rewe, Netto, HIT, EDEKA, Lidl, Aldi Nord, Aldi Süd, Penny; serves them by geographic radius |
| `Services.Prices` | Scrapes per-chain online prices for tracked products, matches chain stores to POI locations, and serves current/historical prices by store or location |
| `frontend` | Nuxt 4 + Leaflet map UI, proxied by the gateway |

Both `Services.POI` and `Services.Prices` persist to a single shared SQLite database
(`monber.db`) — POI owns the `stores` table, Prices owns its own price/store-matching tables and
reads `stores` to match scraped chain stores against POI locations.

Each service follows a vertical-slice layout: routes are mapped in `Features/Endpoints.cs`, with
one static handler class per route (`<Verb><Resource>Endpoint.cs`). See `CLAUDE.md` for the
detailed per-service data flow and conventions.

## Prerequisites

- [.NET SDK 10.0.111+](https://dotnet.microsoft.com/download) (pinned via `global.json`)
- [Node.js](https://nodejs.org/) + npm (for the frontend)
- [Aspire CLI](https://aspire.dev) (optional, only needed to run/publish via the AppHost)
- Docker (optional, only needed for the Docker Compose deployment or to run FlareSolverr locally)

## Running locally

The AppHost is the easiest way to run everything together — it starts both backend services, the
gateway, the frontend (`npm run dev`, installing dependencies first if needed), and a FlareSolverr
container (used by `Services.Prices` to get past Rewe's Cloudflare challenge):

```bash
dotnet run --project MonberAPI.AppHost
```

This opens the Aspire dashboard, from which you can reach the gateway and every service's logs,
traces, and health.

Alternatively, run pieces individually:

```bash
dotnet build MonberAPI.slnx        # build everything
dotnet run --project Services.POI       # POI service only
dotnet run --project Services.Prices    # Prices service only
dotnet run --project MonberAPI.Gateway  # gateway only
cd frontend && npm install && npm run dev
```

Each backend service exposes Scalar API docs at `/docs` and an OpenAPI document at
`/openapi/v1.json` when running standalone.

### MapTiler key (optional)

The frontend's basemap uses MapTiler tiles if a key is available, falling back to plain
OpenStreetMap tiles otherwise. Supply one locally via `MonberAPI.AppHost/MAPTILER_API.key`
(gitignored) or the `Parameters:maptiler-api-key` configuration key / user secret.

### EF Core migrations (Services.POI / Services.Prices)

Both services manage their SQLite schema via EF Core migrations, applied automatically on
startup. After changing an entity or `Context`, add a migration:

```bash
dotnet ef migrations add <Name> --project Services.POI
dotnet ef migrations add <Name> --project Services.Prices
```

## Deployment

Production runs as Docker Compose (`deploy/docker-compose.yaml`), generated from the AppHost via
`aspire publish` and committed by CI (`.github/workflows/publish.yml`) on every push to `main`.
That workflow also builds and pushes each service's image to GHCR
(`ghcr.io/c9glax/monber-{services-poi,services-prices,gateway,frontend}`).

To deploy: fill in `deploy/.env` (`GATEWAY_PORT`, and `MAPTILER_API_KEY` if you have one), then:

```bash
cd deploy
docker compose up -d
```

POI and Prices share one SQLite database via a common bind-mounted `./data` directory (a one-shot
`data-init` container fixes ownership before the app containers start, since Docker auto-creates
the bind mount as root). The gateway is the only container with a published host port.

## Project status

- **Services.POI** — implemented. Syncs store locations from Overpass on startup (re-syncing at
  most once per 24h) and via `POST /stores/update`; serves them via `GET /stores?lat=&lon=`
  (30 km radius, haversine-ordered).
- **Services.Prices** — implemented. Per-chain scrapers (Kaufland, Rewe, Netto, HIT, EDEKA, Lidl,
  Aldi Nord, Aldi Süd, Penny) for a tracked product list, with Rewe's scraper going through
  FlareSolverr to clear Cloudflare. Endpoints: `GET /stores`, `POST /stores/update`,
  `GET /prices`, `GET /prices/store`, `GET /prices/history`.
- **MonberAPI.Gateway** — implemented. Routes `/poi`, `/prices`, and the UI through one origin.
- **frontend** — implemented. Nuxt + Leaflet map UI backed by the gateway's `/poi` and `/prices`
  APIs.
- **Tests** — there are no test projects in this solution currently.

## Repository layout notes

- `deploy/` — generated `docker-compose.yaml` (do not hand-edit; regenerate via `aspire publish`)
  and the `.env` deployers fill in.
- `logs.txt` and `monber.db` in the repo root are local run artifacts, not part of the source.
