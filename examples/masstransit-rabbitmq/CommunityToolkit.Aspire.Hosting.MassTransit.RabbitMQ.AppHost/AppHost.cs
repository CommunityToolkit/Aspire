var builder = DistributedApplication.CreateBuilder(args);

var rmq = builder.AddRabbitMQ(
        name: "rmq",
        port: 5672)
    .WithExternalHttpEndpoints()
    .WithManagementPlugin(port: 15672);

#pragma warning disable ASPIREDOTNETPROJECT001
var api = builder.AddDotnetProject("api", new Projects.CommunityToolkit_Aspire_MassTransit_RabbitMQ_ApiService().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WaitFor(rmq).WithReference(rmq);

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("publisher", new Projects.CommunityToolkit_Aspire_MassTransit_RabbitMQ_Publisher().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WaitFor(api).WaitFor(rmq).WithReference(rmq);

builder.Build().Run();