using CommunityToolkit.Aspire.Hosting.Azure.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("cae").WithDashboard();

var redis = builder.AddAzureRedis("redisState").WithAccessKeyAuthentication().RunAsContainer();

// local development still uses dapr redis state container
var stateStore = builder.AddDaprStateStore("statestore")
                        .WithReference(redis);

var pubSub = builder.AddDaprPubSub("pubsub")
                    .WithMetadata("redisHost", "localhost:6379")
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
