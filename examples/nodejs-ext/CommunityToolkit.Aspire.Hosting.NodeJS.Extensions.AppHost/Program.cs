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
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();

var nxWithNpm = builder.AddNxApp("nx-demo-with-npx", workingDirectory: "../nx-demo")
    .WithNpmPackageInstaller()
    .RunWithPackageManager();

nxWithNpm.AddApp("blog-monorepo-with-npx", appName: "blog-monorepo")
    .WithHttpEndpoint()
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();

// Example of Turborepo monorepo support - uncomment if you have a Turborepo workspace
var turbo = builder.AddTurborepoApp("turborepo-demo")
    .WithNpmPackageInstaller();

turbo.AddApp("turbo-web", filter: "web")
    .WithHttpEndpoint()
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();
turbo.AddApp("turbo-docs", filter: "docs")
    .WithHttpEndpoint()
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();

var turboWithNpx = builder.AddTurborepoApp("turborepo-demo-with-npx", workingDirectory: "../turborepo-demo")
    .WithNpmPackageInstaller()
    .RunWithPackageManager();

turboWithNpx.AddApp("turbo-web-with-npx", filter: "web")
    .WithHttpEndpoint()
    .WithMappedEndpointPort()
    .WithHttpHealthCheck();


builder.Build().Run();
