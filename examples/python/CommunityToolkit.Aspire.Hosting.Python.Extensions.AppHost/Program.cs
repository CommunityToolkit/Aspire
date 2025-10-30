#pragma warning disable CS0612
var builder = DistributedApplication.CreateBuilder(args);

builder.AddUvicornApp("uvicornapp", "../uvicornapp-api", "main:app");

builder.AddUvApp("uvapp", "../uv-api", "uv-api")
    .WithHttpEndpoint(env: "PORT");

builder.Build().Run();