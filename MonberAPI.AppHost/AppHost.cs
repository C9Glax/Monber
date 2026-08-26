using Aspire.Hosting.ApplicationModel;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> poi = builder.AddProject<Projects.Services_POI>("services-poi");

IResourceBuilder<ProjectResource> prices = builder.AddProject<Projects.Services_Prices>("services-prices");

IResourceBuilder<ProjectResource> gateway = builder.AddProject<Projects.MonberAPI_Gateway>("gateway")
    .WithReference(poi)
    .WithReference(prices)
    .WithExternalHttpEndpoints();

builder.AddNpmApp("frontend", "../frontend", "dev")
    .WithReference(gateway)
    .WithEnvironment("NUXT_PUBLIC_API_BASE", gateway.GetEndpoint("http"))
    .WithHttpEndpoint(env: "PORT", targetPort: 3000)
    .WithExternalHttpEndpoints();

builder.Build().Run();
