var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("cae").WithDaprComponents();

var redis = builder.AddAzureRedis("redisState")
                   .RunAsContainer();

// State store using Redis
var stateStore = builder.AddDaprStateStore("statestore")
                        .WithReference(redis)
                        .WithMetadata("actorStateStore", "true");

// PubSub also using Redis - for Azure deployment this will use the same Redis instance
var pubSub = builder.AddDaprPubSub("pubsub")
                    .WithReference(redis)
                    .WaitFor(redis);


builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("servicea")
       .WithDaprSidecar(sidecar => sidecar.WithReference(stateStore).WithReference(pubSub))
       .WaitFor(redis);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceB>("serviceb")
       .WithDaprSidecar(sidecar => sidecar.WithReference(pubSub))
       .WaitFor(redis);

// console app with no appPort (sender only)
builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceC>("servicec")
       .WithDaprSidecar(sidecar => sidecar.WithReference(stateStore))
       .WaitFor(redis);

builder.Build().Run();
