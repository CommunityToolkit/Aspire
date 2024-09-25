var builder = DistributedApplication.CreateBuilder(args);

builder.AddViteApp("vite-demo")
    .WithNpmPackageInstallation();

builder.AddViteApp("yarn-demo", packageManager: "yarn")
    .WithYarnPackageInstallation();

builder.AddViteApp("pnpm-demo", packageManager: "pnpm")
    .WithPnpmPackageInstallation();

builder.Build().Run();
