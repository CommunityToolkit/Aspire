var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis").WithRedisInsight();


var stateStore = builder.AddDaprStateStore("statestore")
    .WaitFor(redis);

var redisHost= redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Host);
var redisPort = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Port);

var pubSub = builder
  .AddDaprPubSub("pubsub")
  .WithMetadata(
    "redisHost",
    ReferenceExpression.Create(
      $"{redisHost}:{redisPort}"
    )
  )
  .WaitFor(redis);

if (redis.Resource.PasswordParameter is not null)
{
    pubSub.WithMetadata("redisPassword", redis.Resource.PasswordParameter);
}

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("servicea", new Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
       .WithDaprSidecar(sidecar =>
       {
           sidecar.WithReference(stateStore).WithReference(pubSub);
       }).WaitFor(redis);

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
