var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddBunApp("api")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/");

builder.Build().Run();
