using Aspire.Hosting.ApplicationModel;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> poi = builder.AddProject<Projects.Services_POI>("services-poi");

IResourceBuilder<ProjectResource> prices = builder.AddProject<Projects.Services_Prices>("services-prices");

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

var frontend = builder.AddNpmApp("frontend", "../frontend", "dev")
    .WithHttpEndpoint(env: "PORT", targetPort: 3000)
    .WithEnvironment("NUXT_PUBLIC_MAP_TILER_KEY", mapTilerApiKey);

// The gateway is the single external entry point: it serves the UI (proxied through to the
// frontend dev server) and the /poi and /prices APIs, all on one origin.
builder.AddProject<Projects.MonberAPI_Gateway>("gateway")
    .WithReference(poi)
    .WithReference(prices)
    .WithReference(frontend)
    .WithExternalHttpEndpoints();

builder.Build().Run();
