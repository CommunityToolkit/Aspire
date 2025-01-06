var builder = DistributedApplication.CreateBuilder(args);


var redisState = builder.AddAzureRedis("redisState").RunAsContainer();

// This currently only effects publishing
// local development still uses dapr redis state container
var daprState = builder.AddDaprStateStore("daprState")
    .WithReference(redisState); // <= This instructs the application to configure dapr to use the redis state store

// API does not provide any functional example of Dapr - it simply demonstrates referencing the dapr state
var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_AzureRedis_ApiService>("example-api")
    .WithReference(daprState)
    .WithDaprSidecar();

builder.Build().Run();
