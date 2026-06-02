#pragma warning disable CS0618

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddBunApp("api")
    .WithBunPackageInstallation()
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/");

builder.Build().Run();

#pragma warning restore CS0618
