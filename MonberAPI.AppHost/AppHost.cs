using Aspire.Hosting.ApplicationModel;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> poi = builder.AddProject<Projects.Services_POI>("services-poi");

IResourceBuilder<ProjectResource> prices = builder.AddProject<Projects.Services_Prices>("services-prices");

builder.AddProject<Projects.MonberAPI_Gateway>("gateway")
    .WithReference(poi)
    .WithReference(prices)
    .WithExternalHttpEndpoints();

builder.Build().Run();
