
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);


var redis = builder.AddAzureRedis("redisState").WithAccessKeyAuthentication().RunAsContainer();

// local development still uses dapr redis state container
var stateStore = builder.AddDaprStateStore("statestore")
                        .WithReference(redis);

var pubSub = builder.AddDaprPubSub("pubsub")
                    .WithMetadata("redisHost", "localhost:6379")
                    .WaitFor(redis);


builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("servicea")
       .PublishAsAzureContainerApp((infrastructure, container) => container.WithDaprSidecar()) // Demonstration - not actually required
       .WithReference(stateStore)
       .WithReference(pubSub)
       .WithDaprSidecarOptions(new DaprSidecarOptions { LogLevel = "Information" }) // LogLevel.Information TODO: Update Dapr Sidecar options to use LogLevel 
       //.WithDaprSidecar()
       .WaitFor(redis);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceB>("serviceb")
       .WithReference(pubSub)
       //.WithDaprSidecar()
       .WaitFor(redis);

// console app with no appPort (sender only)
builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceC>("servicec")
       .WithReference(stateStore)
       //.WithDaprSidecar()
       .WaitFor(redis);

builder.Build().Run();
