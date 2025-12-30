var builder = DistributedApplication.CreateBuilder(args);

var nx = builder.AddNxApp("nx-demo")
    .WithNpm(install: true)
    .WithPackageManagerLaunch();

nx.AddApp("blog-monorepo")
    .WithHttpEndpoint(env: "PORT")
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();

var turbo = builder.AddTurborepoApp("turborepo-demo")
    .WithNpm(install: true)
    .WithPackageManagerLaunch();

turbo.AddApp("turbo-web", filter: "web")
    .WithHttpEndpoint(env: "PORT")
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();

turbo.AddApp("turbo-docs", filter: "docs")
    .WithHttpEndpoint(env: "PORT")
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();

builder.Build().Run();
