var builder = DistributedApplication.CreateBuilder(args);

var golang = builder.AddGolangApp("golang", "../gin-api")
    .WithHttpEndpoint(env: "PORT");

builder.Build().Run();