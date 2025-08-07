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
var nx = builder.AddNxApp("nx-workspace", workingDirectory: "../frontend")
    .WithNpmPackageInstaller();
// 
// var app1 = nx.AddApp("app1")
//     .WithHttpHealthCheck();
// var app2 = nx.AddApp("app2", appName: "my-app-2")
//     .WithHttpHealthCheck();

// Example of Turborepo monorepo support - uncomment if you have a Turborepo workspace
// var turbo = builder.AddTurborepoApp("turbo-workspace", workingDirectory: "../frontend")
//     .WithYarnPackageInstaller();
// 
// var turboApp1 = turbo.AddApp("turbo-app1")
//     .WithHttpHealthCheck();
// var turboApp2 = turbo.AddApp("turbo-app2", filter: "custom-filter")
//     .WithHttpHealthCheck();

builder.Build().Run();
