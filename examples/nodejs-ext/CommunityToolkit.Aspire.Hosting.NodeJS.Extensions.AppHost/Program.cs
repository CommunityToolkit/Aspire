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

builder.Build().Run();
