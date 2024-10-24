using CommunityToolkit.Aspire.Hosting.Deno;

var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddDenoTask("vite-demo", taskName: "dev")
    .WithDenoPackageInstallation()
    .WithHttpEndpoint(env: "PORT")
    .WithEndpoint();

builder.AddDenoApp("oak-demo", "main.ts", permissionFlags: ["-E", "--allow-net"])
    .WithDenoPackageInstallation()
    .WithHttpEndpoint(env: "PORT")
    .WithEndpoint();

builder.Build().Run();
