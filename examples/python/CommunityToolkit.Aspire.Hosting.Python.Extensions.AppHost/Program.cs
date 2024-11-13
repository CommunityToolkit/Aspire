var builder = DistributedApplication.CreateBuilder(args);

var uvicorn = builder.AddUvicornApp("uvicornapp", "../uvicornapp-api", "main:app")
    .WithHttpEndpoint(env: "UVICORN_PORT");

builder.Build().Run();