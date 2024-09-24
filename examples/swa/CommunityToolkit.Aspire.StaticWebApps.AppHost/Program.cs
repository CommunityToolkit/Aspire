var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.CommunityToolkit_Aspire_StaticWebApps_ApiApp>("api");

var web = builder
    .AddNpmApp("web", Path.Combine("..", "CommunityToolkit.Aspire.StaticWebApps.WebApp"), "dev")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

_ = builder
    .AddSwaEmulator("swa", new()
    {
        DevServerTimeout = TimeSpan.FromMinutes(5).Seconds,
    })
    .WithAppResource(web)
    .WithApiResource(api);

builder.Build().Run();
