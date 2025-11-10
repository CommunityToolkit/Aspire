#pragma warning disable CS0612 // Type or member is obsolete
#pragma warning disable CS0618 // Type or member is obsolete
var builder = DistributedApplication.CreateBuilder(args);

builder.AddUvicornApp("uvicornapp", "../uvicornapp-api", "main:app");

builder.AddUvApp("uvapp", "../uv-api", "uv-api")
    .WithHttpEndpoint(env: "PORT");

builder.AddStreamlitApp("streamlitapp", "../streamlit-api", "app.py")
    .WithHttpEndpoint(env: "PORT");

builder.Build().Run();