<img src="frontend/public/can.svg" width="28" height="28" align="left" alt="">

# Monber

[![Publish images and compose](https://github.com/C9Glax/Monber/actions/workflows/publish.yml/badge.svg)](https://github.com/C9Glax/Monber/actions/workflows/publish.yml)

<br clear="left">

MonberAPI is a .NET Aspire solution for tracking German grocery store locations and energy-drink can prices, with a Nuxt frontend served through a reverse-proxy gateway.

See [CLAUDE.md](CLAUDE.md) for architecture details and development commands, and [frontend/README.md](frontend/README.md) for frontend-specific setup.

## Dependencies

**.NET / NuGet**
- [Aspire.Hosting.Docker](https://www.nuget.org/packages/Aspire.Hosting.Docker) & [Aspire.Hosting.NodeJs](https://www.nuget.org/packages/Aspire.Hosting.NodeJs) — Aspire orchestration
- [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore) & [Microsoft.EntityFrameworkCore.Sqlite](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite) — data access
- [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite) — SQLite driver
- [Microsoft.AspNetCore.OpenApi](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi) — OpenAPI generation
- [Scalar.AspNetCore](https://www.nuget.org/packages/Scalar.AspNetCore) & [Scalar.Aspire](https://www.nuget.org/packages/Scalar.Aspire) — API docs UI
- [Yarp.ReverseProxy](https://www.nuget.org/packages/Yarp.ReverseProxy) — gateway reverse proxy
- [Microsoft.Extensions.ServiceDiscovery](https://www.nuget.org/packages/Microsoft.Extensions.ServiceDiscovery) & [Microsoft.Extensions.ServiceDiscovery.Yarp](https://www.nuget.org/packages/Microsoft.Extensions.ServiceDiscovery.Yarp) — service discovery
- [Microsoft.Extensions.Http.Resilience](https://www.nuget.org/packages/Microsoft.Extensions.Http.Resilience) — resilient HTTP clients
- [OpenTelemetry.Extensions.Hosting](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting) and the OpenTelemetry instrumentation/exporter packages — observability

**Frontend / npm**
- [Nuxt](https://nuxt.com/) & [Vue](https://vuejs.org/) — application framework
- [vue-router](https://router.vuejs.org/) — routing
- [Leaflet](https://leafletjs.com/) — interactive maps
- [Phosphor Icons](https://phosphoricons.com/) — icon set
