using MonberAPI.ServiceDefaults;

const string FrontendCorsPolicy = "Frontend";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

string[] allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                           ?? ["http://localhost:3000"];

builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

app.UseCors(FrontendCorsPolicy);

app.MapReverseProxy();

app.Run();
