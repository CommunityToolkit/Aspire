var builder = DistributedApplication.CreateBuilder(args);

var rmq = builder.AddRabbitMQ(
        name: "rmq",
        port: 5672)
    .WithExternalHttpEndpoints()
    .WithManagementPlugin(port: 15672);

var api = builder.AddProject<Projects.CommunityToolkit_Aspire_MassTransit_RabbitMQ_ApiService>("api")
    .WaitFor(rmq).WithReference(rmq);

builder.AddProject<Projects.CommunityToolkit_Aspire_MassTransit_RabbitMQ_Publisher>("publisher")
    .WaitFor(api).WaitFor(rmq).WithReference(rmq);

builder.Build().Run();