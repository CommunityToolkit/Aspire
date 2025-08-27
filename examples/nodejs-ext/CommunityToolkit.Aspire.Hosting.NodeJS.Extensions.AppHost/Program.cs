var builder = DistributedApplication.CreateBuilder(args);

builder.AddViteApp("vite-demo")
    .WithNpmPackageInstallation()
    .WithHttpHealthCheck();

builder.AddViteApp("yarn-demo", packageManager: "yarn")
    .WithYarnPackageInstallation()
    .WithHttpHealthCheck();

builder.AddViteApp("pnpm-demo", packageManager: "pnpm")
    .WithPnpmPackageInstallation()
    .WithHttpHealthCheck();

// Example of Nx monorepo support - uncomment if you have an Nx workspace
var nx = builder.AddNxApp("nx-demo")
    .WithNpmPackageInstaller();

nx.AddApp("blog-monorepo")
    .WithHttpEndpoint()
    .WithHttpHealthCheck();
// var app2 = nx.AddApp("app2", appName: "my-app-2")
//     .WithHttpHealthCheck();

// Example of Turborepo monorepo support - uncomment if you have a Turborepo workspace
var turbo = builder.AddTurborepoApp("turborepo-demo")
    .WithNpmPackageInstaller();

turbo.AddApp("turbo-web", filter: "web")
    .WithHttpEndpoint()
    .WithMappedEndpointPort();
turbo.AddApp("turbo-docs", filter: "docs")
    .WithHttpEndpoint()
    .WithMappedEndpointPort();

builder.Build().Run();
