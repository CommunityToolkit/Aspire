var builder = DistributedApplication.CreateBuilder(args);


var passwordParam = builder.AddParameter("RabbitMQPassword", secret: true);

var rmq = builder.AddMassTransitRabbitMq("RabbitMQInstance", options =>
{
    options.Port = 990;
});


builder.AddProject<Projects.CommunityToolkit_Aspire_Client_MassTransit_RabbitMQ_ApiService>("api")
    .WaitFor(rmq).WithReference(rmq);

builder.Build().Run();