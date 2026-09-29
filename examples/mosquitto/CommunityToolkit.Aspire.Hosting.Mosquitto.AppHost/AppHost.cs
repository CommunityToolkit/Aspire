var builder = DistributedApplication.CreateBuilder(args);

var mqtt = builder.AddMosquitto("mqtt");

builder.Build().Run();
