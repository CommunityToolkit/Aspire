using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var lavinmq = builder.AddLavinMQ("lavinmq")
    .PublishAsConnectionString();

builder.AddProject<CommunityToolkit_Aspire_Hosting_LavinMQ_MassTransit>("masstransitExample")
    .WithReference(lavinmq)
    .WithHttpHealthCheck(path: "/health")
    .WaitFor(lavinmq);

builder.Build().Run();
