var builder = DistributedApplication.CreateBuilder(args);

var rmq = builder.AddRabbitMQ("rabbitMQ")
                   .WithManagementPlugin()
                   .WithEndpoint("tcp", e => e.Port = 5672)
                   .WithEndpoint("management", e => e.Port = 15672);


var stateStore = builder.AddDaprStateStore("statestore");

var pubSub = builder.AddDaprPubSub("pubsub")
                           .WithMetadata("password", rmq.Resource.PasswordParameter)
                    .WaitFor(rmq);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("servicea")
       .WithReference(stateStore)
       .WithReference(pubSub)
       .WithDaprSidecar()
       .WaitFor(rmq);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceB>("serviceb")
       .WithReference(pubSub)
       .WithDaprSidecar()
       .WaitFor(rmq);

// console app with no appPort (sender only)
builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceC>("servicec")
       .WithReference(stateStore)
       .WithDaprSidecar();

builder.Build().Run();
