using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// POI and Prices share one SQLite file (see the WaitFor(poi) comment below) via a plain relative
// path when run locally with `dotnet run` from their own project directories. In docker-compose
// they're separate containers with independent filesystems, so that trick can't work there - this
// instead points both containers at the same bind-mounted host directory, running them as a fixed
// non-root UID:GID so they can actually write into it.
//
// The bind-mounted directory itself is *not* reliably owned by that UID: Docker Engine runs as
// root and auto-creates a missing bind-mount source directory owned by root:root, regardless of
// which host user ran `docker compose up` - so a plain `user: "1000:1000"` on the app containers
// still hits SQLite Error 14 ("unable to open database file") on a fresh deploy. dataInit below is
// a one-shot root container that chowns ./data to 1000:1000 before either app container starts, so
// ownership no longer depends on who created the directory or what UID they happen to have.
IResourceBuilder<ContainerResource>? dataInit = null;
if (builder.ExecutionContext.IsPublishMode)
{
    dataInit = builder.AddContainer("data-init", "busybox", "latest")
        .WithEntrypoint("sh")
        .WithArgs("-c", "chown 1000:1000 /data")
        .PublishAsDockerComposeService((_, service) =>
        {
            service.Volumes.Add(new Volume
            {
                Name = "monber-db",
                Type = "bind",
                Source = "./data",
                Target = "/data",
            });
        });
}

void ApplySharedDbVolume(IResourceBuilder<ProjectResource> resource)
{
    if (!builder.ExecutionContext.IsPublishMode)
    {
        // Local `dotnet run` orchestration has no /data directory - leave Program.cs's default
        // "../monber.db" (shared via CWD) connection string untouched.
        return;
    }

    resource
        .WithEnvironment("ConnectionStrings__MonberDb", "Data Source=/data/monber.db")
        .WaitForCompletion(dataInit!)
        .PublishAsDockerComposeService((_, service) =>
        {
            service.User = "1000:1000";
            service.Volumes.Add(new Volume
            {
                Name = "monber-db",
                Type = "bind",
                Source = "./data",
                Target = "/data",
            });
        });
}

// Docker Compose publish target: `aspire publish` emits deploy/docker-compose.yaml wiring every
// resource together. Every project resource is a "requires image build" resource as far as Aspire
// is concerned, so the compose file references each one by an externally-supplied ${..._IMAGE} env
// var rather than a literal tag - the GitHub Actions workflow builds+pushes the actual images to
// GHCR and writes deploy/.env with the real ghcr.io/... references. AddDockerComposeEnvironment is
// a no-op outside publish mode, so it's safe to always add.
builder.AddDockerComposeEnvironment("monber");

// WithHttpHealthCheck lets the gateway's WaitFor below block on more than just "process started" -
// for POI in particular, /health only turns healthy once the startup Overpass store sync finishes
// (see OverpassSyncHealthCheck in Services.POI).
IResourceBuilder<ProjectResource> poi = builder.AddProject<Projects.Services_POI>("services-poi")
    .WithHttpHealthCheck("/health");
ApplySharedDbVolume(poi);

// REWE sits behind Cloudflare; Services.Prices solves the challenge via FlareSolverr
// (https://github.com/FlareSolverr/FlareSolverr), which Aspire runs as its own container here so
// no separately-managed instance is needed. Its dynamically-assigned endpoint is handed to
// services-prices as FlareSolverr__Url, the same config key ReweePriceFetcher already reads.
IResourceBuilder<ContainerResource> flaresolverr = builder
    .AddContainer("flaresolverr", "ghcr.io/flaresolverr/flaresolverr", "latest")
    .WithHttpEndpoint(targetPort: 8191, name: "http");

// Prices' own startup store sync (see StoreSyncStatus) matches chain stores against the shared
// `stores` table that POI owns - if it ran before POI's Overpass sync populated that table, every
// brand would be skipped with no DbStoreExternalId rows ever written until someone manually hits
// POST /update-stores again. WaitFor(poi) holds this service's own startup back until POI is healthy,
// so its first (and normally only) sync attempt actually has stores to match against. WaitFor(flaresolverr)
// similarly holds it back until the FlareSolverr container is running.
IResourceBuilder<ProjectResource> prices = builder.AddProject<Projects.Services_Prices>("services-prices")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("FlareSolverr__Url", flaresolverr.GetEndpoint("http"))
    .WaitFor(flaresolverr)
    .WaitFor(poi);
ApplySharedDbVolume(prices);

// The MapTiler key is never baked into the published compose file - it's a per-deployment value.
// Locally (dotnet run) it still resolves from MonberAPI.AppHost/MAPTILER_API.key (gitignored, see
// *.key in .gitignore) or Parameters:maptiler-api-key config/user-secrets, falling back to empty
// (MapView.client.vue falls back to plain OSM tiles when unset). Using AddParameter's Func<string>
// overload rather than a plain string means this resolver only runs when something actually needs
// the value (e.g. setting the npm dev process's env var) - in docker-compose publish mode it's
// never invoked at all: Aspire instead emits a NUXT_PUBLIC_MAP_TILER_KEY=${MAPTILER_API_KEY}
// reference in docker-compose.yaml plus a blank MAPTILER_API_KEY= placeholder line in deploy/.env,
// so CI never needs the key and each deployer fills in their own before `docker compose up`.
string mapTilerKeyFile = Path.Combine(builder.AppHostDirectory, "MAPTILER_API.key");
IResourceBuilder<ParameterResource> mapTilerKeyParam = builder.AddParameter(
    "maptiler-api-key",
    () => File.Exists(mapTilerKeyFile)
        ? File.ReadAllText(mapTilerKeyFile).Trim()
        : builder.Configuration["Parameters:maptiler-api-key"] ?? "",
    publishValueAsDefault: false,
    secret: false)
    .WithDescription("MapTiler API key for the basemap tiles (https://cloud.maptiler.com/). Leave empty to fall back to plain OpenStreetMap tiles.");

// Locally (dotnet run) the frontend runs as a plain `npm run dev` process. For docker-compose
// publish it's instead represented as a Dockerfile-backed resource (frontend/Dockerfile, a
// production Nuxt build) so Aspire treats it the same as the .NET projects - image build required,
// referenced via ${FRONTEND_IMAGE} in the generated compose file - even though the actual image is
// built and pushed to GHCR by the GitHub Actions workflow, not by `aspire publish` itself.
IResourceBuilder<IResourceWithEndpoints> frontend;
if (builder.ExecutionContext.IsPublishMode)
{
    frontend = builder.AddDockerfile("frontend", "../frontend")
        .WithHttpEndpoint(targetPort: 3000)
        .WithEnvironment("NUXT_PUBLIC_MAP_TILER_KEY", mapTilerKeyParam);
}
else
{
    // npm run dev requires node_modules to already be present, so install dependencies first and
    // have the dev server wait for that to finish rather than failing on a fresh checkout.
    IResourceBuilder<ExecutableResource> frontendInstall = builder.AddExecutable("frontend-npm-install", "npm", "../frontend", "install")
        .ExcludeFromManifest();

    IResourceBuilder<NodeAppResource> frontendDev = builder.AddNpmApp("frontend", "../frontend", "dev")
        .WaitForCompletion(frontendInstall)
        .WithHttpEndpoint(env: "PORT", targetPort: 3000)
        .WithEnvironment("NUXT_PUBLIC_MAP_TILER_KEY", mapTilerKeyParam);

    frontendInstall.WithParentRelationship(frontendDev);
    frontend = frontendDev;
}

// The gateway is the single external entry point: it serves the UI (proxied through to the
// frontend dev server) and the /poi and /prices APIs, all on one origin. WaitFor holds it back
// until both backing services report healthy, so it doesn't start proxying requests to a POI
// service that's still mid Overpass-sync.
builder.AddProject<Projects.MonberAPI_Gateway>("gateway")
    .WithReference(poi)
    .WithReference(prices)
    .WithReference(frontend.GetEndpoint("http"))
    .WaitFor(poi)
    .WaitFor(prices)
    .WithExternalHttpEndpoints();

builder.Build().Run();
