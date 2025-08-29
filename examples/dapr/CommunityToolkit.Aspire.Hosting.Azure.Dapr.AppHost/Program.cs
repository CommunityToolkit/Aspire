var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("cae").WithDaprComponents();

var redis = builder.AddAzureRedis("redisState").WithAccessKeyAuthentication().RunAsContainer();

// State store using Redis
var stateStore = builder.AddDaprStateStore("statestore")
                        .WithReference(redis);

// PubSub also using Redis - for Azure deployment this will use the same Redis instance
// For local development, it uses the .WithMetadata for local Redis configuration
var pubSub = builder.AddDaprPubSub("pubsub")
                    .WithReference(redis)  // This enables Azure Redis pubsub deployment
                    .WithMetadata("redisHost", "localhost:6379")  // This is for local development
                    .WaitFor(redis);


builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("servicea")
       .WithDaprSidecar(sidecar =>
       {
           sidecar.WithReference(stateStore).WithReference(pubSub);
       }).WaitFor(redis);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceB>("serviceb")
       .WithDaprSidecar(sidecar => sidecar.WithReference(pubSub))
       .WaitFor(redis);

// console app with no appPort (sender only)
builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceC>("servicec")
       .WithDaprSidecar(sidecar => sidecar.WithReference(stateStore))
       .WaitFor(redis);

builder.Build().Run();
