IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Services_POI>("services-poi");

builder.AddProject<Projects.Services_Prices>("services-prices");

builder.Build().Run();
