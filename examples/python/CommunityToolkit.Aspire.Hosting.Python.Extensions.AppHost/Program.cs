var builder = DistributedApplication.CreateBuilder(args);

var uvicorn = builder.AddUvicornApp("uvicornapp", "../uvicornapp-api", "main:app")
    .WithHttpEndpoint(env: "UVICORN_PORT");

var uv = builder.AddUvApp("uvapp", "../uv-api", "uv-api")
    .WithHttpEndpoint(env: "PORT");

builder.Build().Run();