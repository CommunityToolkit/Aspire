var builder = DistributedApplication.CreateBuilder(args);

var redisState = builder.AddAzureRedis("redisState").RunAsContainer();

var daprState = builder.AddDaprStateStore("daprState")
    .WithReference(redisState);

var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_AzureRedis_ApiService>("example-api")
    .WithReference(daprState)
    .WithDaprSidecar();

builder.Build().Run();
