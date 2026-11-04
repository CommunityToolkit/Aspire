var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("cae").WithDaprComponents();

var redis = builder.AddAzureManagedRedis("redisState")
                   .RunAsContainer();

// State store using Redis
var stateStore = builder.AddDaprStateStore("statestore")
                        .WithReference(redis)
                        .WithMetadata("actorStateStore", "true");

// PubSub also using Redis - for Azure deployment this will use the same Redis instance
var pubSub = builder.AddDaprPubSub("pubsub")
                    .WithReference(redis)
                    .WaitFor(redis);


#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("servicea", new Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
       .PublishAsAzureContainerApp((i,c)=> { })
       .WithDaprSidecar(sidecar => sidecar.WithReference(stateStore).WithReference(pubSub))
       .WaitFor(redis);

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("serviceb", new Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceB().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
       .WithDaprSidecar(sidecar => sidecar.WithReference(pubSub))
       .WaitFor(redis);

// console app with no appPort (sender only)
#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("servicec", new Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceC().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
       .WithDaprSidecar(sidecar => sidecar.WithReference(stateStore))
       .WaitFor(redis);

builder.Build().Run();
