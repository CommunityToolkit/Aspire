using CommunityToolkit.Aspire.Hosting.MassTransit.RabbitMQ;

var builder = DistributedApplication.CreateBuilder(args);


var rmq = builder.AddMassTransit("RabbitMQInstance", options =>
{
    options.Username = "guest";
    options.Password = "guest";
    options.Port = 5672;
});


builder.AddProject<Projects.CommunityToolkit_Aspire_Client_MassTransit_RabbitMQ_ApiService>("api")
    .WaitFor(rmq).WithReference(rmq);

builder.Build().Run();