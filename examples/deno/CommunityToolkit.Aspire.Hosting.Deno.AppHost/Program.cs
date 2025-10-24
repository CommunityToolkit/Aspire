var builder = DistributedApplication.CreateBuilder(args);

builder.AddDenoTask("vite-demo", taskName: "dev")
    .WithDenoPackageInstallation()
    .WithHttpEndpoint(env: "PORT")
    .WithEndpoint()
    .WithHttpHealthCheck("/");

builder.AddDenoApp("oak-demo", "main.ts", permissionFlags: ["--allow-env", "--allow-net"])
    .WithHttpEndpoint(env: "PORT")
    .WithEndpoint()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
