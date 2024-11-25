var builder = DistributedApplication.CreateBuilder(args);


var rmq = builder.AddRabbitMQ(
        name: "rmq",
        port: 5672)
    .WithExternalHttpEndpoints()
    .WithManagementPlugin();


builder.AddProject<Projects.CommunityToolkit_Aspire_Client_MassTransit_RabbitMQ_ApiService>("api")
    .WaitFor(rmq).WithReference(rmq);

builder.Build().Run();