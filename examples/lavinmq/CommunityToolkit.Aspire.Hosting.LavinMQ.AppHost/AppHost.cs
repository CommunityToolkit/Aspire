using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var lavinmq = builder.AddLavinMQ("lavinmq");

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("masstransitExample", new CommunityToolkit_Aspire_Hosting_LavinMQ_MassTransit().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(lavinmq)
    .WithHttpHealthCheck(path: "/health")
    .WaitFor(lavinmq);

builder.Build().Run();
