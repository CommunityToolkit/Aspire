var builder = DistributedApplication.CreateBuilder(args);

var golang = builder.AddGolangApp("golang", "../gin-api");

builder.Build().Run();