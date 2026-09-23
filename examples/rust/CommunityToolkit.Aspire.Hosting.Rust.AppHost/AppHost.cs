var builder = DistributedApplication.CreateBuilder(args);

builder.AddRustApp("rust-app", "../actix_api")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/");

builder.Build().Run();
