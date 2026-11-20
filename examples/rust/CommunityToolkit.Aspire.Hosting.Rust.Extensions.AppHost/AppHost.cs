var builder = DistributedApplication.CreateBuilder(args);

builder.AddRustApp("rust-app", "../actix_api")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/");

if (builder.Configuration["ENABLE_BACON"] == "1")
{
    builder.AddBaconApp("bacon-app", "../actix_api", ["--headless", "run-long"])
        .WithHttpEndpoint(env: "PORT")
        .WithHttpHealthCheck("/ping");
}

builder.Build().Run();
