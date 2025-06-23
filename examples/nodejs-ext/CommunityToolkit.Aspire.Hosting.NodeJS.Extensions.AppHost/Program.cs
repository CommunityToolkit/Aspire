var builder = DistributedApplication.CreateBuilder(args);

builder.AddViteApp("vite-demo")
    .WithNpmPackageInstallation();

builder.AddViteApp("yarn-demo", packageManager: "yarn")
    .WithYarnPackageInstallation();

builder.AddViteApp("pnpm-demo", packageManager: "pnpm")
    .WithPnpmPackageInstallation();

// Example of using custom args - useful for legacy packages
builder.AddNpmApp("npm-with-flags", "../vite-demo")
    .WithNpmPackageInstallation(useCI: false, args: ["--legacy-peer-deps"])
    .WithHttpEndpoint(env: "PORT");

builder.Build().Run();
