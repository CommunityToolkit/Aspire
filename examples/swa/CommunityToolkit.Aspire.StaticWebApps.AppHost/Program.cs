var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.CommunityToolkit_Aspire_StaticWebApps_ApiApp>("api")
    .WithHttpHealthCheck("/health");

var web = builder
    .AddNpmApp("web", Path.Combine("..", "CommunityToolkit.Aspire.StaticWebApps.WebApp"), "dev")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/");

_ = builder
    .AddSwaEmulator("swa")
    .WithAppResource(web)
    .WithApiResource(api);

builder.Build().Run();
