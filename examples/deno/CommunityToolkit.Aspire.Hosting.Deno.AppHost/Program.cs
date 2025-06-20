using CommunityToolkit.Aspire.Hosting.Deno;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDenoTask("vite-demo", taskName: "dev")
    .WithDenoPackageInstallation()
    .WithHttpEndpoint(env: "PORT")
    .WithEndpoint()
    .WithHttpHealthCheck("/");

builder.AddDenoApp("oak-demo", "main.ts", permissionFlags: ["-E", "--allow-net"])
    .WithHttpEndpoint(env: "PORT")
    .WithEndpoint()
    .WithHttpHealthCheck("/");

builder.Build().Run();
