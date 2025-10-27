var builder = DistributedApplication.CreateBuilder(args);

// Yarn and pnpm support examples
builder.AddYarnApp("yarn-demo", "../yarn-demo")
    .WithYarnPackageInstallation()
    .WithHttpEndpoint()
    .WithHttpHealthCheck();

builder.AddPnpmApp("pnpm-demo", "../pnpm-demo")
    .WithPnpmPackageInstallation()
    .WithHttpEndpoint()
    .WithHttpHealthCheck();

// Example of Nx monorepo support with yarn - uncomment if you have an Nx workspace
var nx = builder.AddNxApp("nx-demo")
    .WithYarnPackageInstaller();

nx.AddApp("blog-monorepo")
    .WithHttpEndpoint()
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();

// Example of Turborepo monorepo support with pnpm - uncomment if you have a Turborepo workspace
var turbo = builder.AddTurborepoApp("turborepo-demo")
    .WithPnpmPackageInstaller();

turbo.AddApp("turbo-web", filter: "web")
    .WithHttpEndpoint()
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();
turbo.AddApp("turbo-docs", filter: "docs")
    .WithHttpEndpoint()
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();

builder.Build().Run();
