using Aspire.Hosting.ApplicationModel;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// WithHttpHealthCheck lets the gateway's WaitFor below block on more than just "process started" -
// for POI in particular, /health only turns healthy once the startup Overpass store sync finishes
// (see OverpassSyncHealthCheck in Services.POI).
IResourceBuilder<ProjectResource> poi = builder.AddProject<Projects.Services_POI>("services-poi")
    .WithHttpHealthCheck("/health");

// Prices' own startup store sync (see StoreSyncStatus) matches chain stores against the shared
// `stores` table that POI owns - if it ran before POI's Overpass sync populated that table, every
// brand would be skipped with no DbStoreExternalId rows ever written until someone manually hits
// POST /update-stores again. WaitFor(poi) holds this service's own startup back until POI is healthy,
// so its first (and normally only) sync attempt actually has stores to match against.
IResourceBuilder<ProjectResource> prices = builder.AddProject<Projects.Services_Prices>("services-prices")
    .WithHttpHealthCheck("/health")
    .WaitFor(poi);

// The MapTiler key is never committed to source (*.key is gitignored). Read it from
// MonberAPI.AppHost/MAPTILER_API.key if present, else Parameters:maptiler-api-key config
// (e.g. `dotnet user-secrets set Parameters:maptiler-api-key <key> --project MonberAPI.AppHost`),
// else fall back to empty - read directly rather than via AddParameter (which throws and takes
// the whole resource down when unset) so the frontend still starts and falls back to plain OSM
// tiles until a key is configured.
string mapTilerKeyFile = Path.Combine(builder.AppHostDirectory, "MAPTILER_API.key");
string mapTilerApiKey = File.Exists(mapTilerKeyFile)
    ? File.ReadAllText(mapTilerKeyFile).Trim()
    : builder.Configuration["Parameters:maptiler-api-key"] ?? "";

// npm run dev requires node_modules to already be present, so install dependencies first and
// have the dev server wait for that to finish rather than failing on a fresh checkout.
IResourceBuilder<ExecutableResource> frontendInstall = builder.AddExecutable("frontend-npm-install", "npm", "../frontend", "install")
    .ExcludeFromManifest();

var frontend = builder.AddNpmApp("frontend", "../frontend", "dev")
    .WaitForCompletion(frontendInstall)
    .WithHttpEndpoint(env: "PORT", targetPort: 3000)
    .WithEnvironment("NUXT_PUBLIC_MAP_TILER_KEY", mapTilerApiKey);

frontendInstall.WithParentRelationship(frontend);

// The gateway is the single external entry point: it serves the UI (proxied through to the
// frontend dev server) and the /poi and /prices APIs, all on one origin. WaitFor holds it back
// until both backing services report healthy, so it doesn't start proxying requests to a POI
// service that's still mid Overpass-sync.
builder.AddProject<Projects.MonberAPI_Gateway>("gateway")
    .WithReference(poi)
    .WithReference(prices)
    .WithReference(frontend)
    .WaitFor(poi)
    .WaitFor(prices)
    .WithExternalHttpEndpoints();

builder.Build().Run();
