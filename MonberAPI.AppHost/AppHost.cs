using Aspire.Hosting.ApplicationModel;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> poi = builder.AddProject<Projects.Services_POI>("services-poi");

IResourceBuilder<ProjectResource> prices = builder.AddProject<Projects.Services_Prices>("services-prices");

var frontend = builder.AddNpmApp("frontend", "../frontend", "dev")
    .WithHttpEndpoint(env: "PORT", targetPort: 3000);

// The gateway is the single external entry point: it serves the UI (proxied through to the
// frontend dev server) and the /poi and /prices APIs, all on one origin.
builder.AddProject<Projects.MonberAPI_Gateway>("gateway")
    .WithReference(poi)
    .WithReference(prices)
    .WithReference(frontend)
    .WithExternalHttpEndpoints();

builder.Build().Run();
