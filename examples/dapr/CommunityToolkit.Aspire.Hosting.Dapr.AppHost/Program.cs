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
