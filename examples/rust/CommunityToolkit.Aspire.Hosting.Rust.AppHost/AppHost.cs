var builder = DistributedApplication.CreateBuilder(args);

builder.AddRustApp("rust-app", "../actix_api")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/");

if (Environment.GetEnvironmentVariable("ENABLE_BACON") == "1")
{
    builder.AddBaconApp("bacon-app", "../actix_api")
        .WithHttpEndpoint(env: "PORT");
}

builder.Build().Run();
