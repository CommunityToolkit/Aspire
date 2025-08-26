var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis").WithRedisInsight();


var stateStore = builder.AddDaprStateStore("statestore")
    .WaitFor(redis);

var redisHost= redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Host);
var redisTargetPort = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.TargetPort);

var pubSub = builder
  .AddDaprPubSub("pubsub")
  .WithMetadata(
    "redisHost",
    ReferenceExpression.Create(
      $"{redisHost}:{redisTargetPort}"
    )
  )
  .WaitFor(redis);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("servicea")
       .WithReference(stateStore)
       .WithReference(pubSub)
       .WithDaprSidecar()
       .WaitFor(pubSub);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceB>("serviceb")
       .WithReference(pubSub)
       .WithDaprSidecar()
       .WaitFor(pubSub);

// console app with no appPort (sender only)
builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceC>("servicec")
       .WithReference(stateStore)
       .WithDaprSidecar()
       .WaitFor(stateStore);

builder.Build().Run();
