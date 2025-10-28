var builder = DistributedApplication.CreateBuilder(args);

// Example of Nx monorepo support with yarn - uncomment if you have an Nx workspace
var nx = builder.AddNxApp("nx-demo");

nx.AddApp("blog-monorepo")
    .WithHttpEndpoint(env: "PORT")
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();

// Example of Turborepo monorepo support with pnpm - uncomment if you have a Turborepo workspace
var turbo = builder.AddTurborepoApp("turborepo-demo");

turbo.AddApp("turbo-web", filter: "web")
    .WithHttpEndpoint(env: "PORT")
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();
turbo.AddApp("turbo-docs", filter: "docs")
    .WithHttpEndpoint(env: "PORT")
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();

builder.Build().Run();
