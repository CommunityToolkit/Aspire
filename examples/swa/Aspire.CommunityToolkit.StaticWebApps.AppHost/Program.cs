var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Aspire_CommunityToolkit_StaticWebApps_ApiApp>("api");

var web = builder
    .AddNpmApp("web", Path.Combine("..", "Aspire.CommunityToolkit.StaticWebApps.WebApp"), "dev")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

_ = builder
    .AddSwaEmulator("swa")
    .WithAppResource(web)
    .WithApiResource(api);

builder.Build().Run();
