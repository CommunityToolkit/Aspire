using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("user", "admin");
var password = builder.AddParameter("password", "admin", secret: true);

var amq = builder.AddActiveMQ("amq", username, password, 61616, "activemq")
        .PublishAsConnectionString()
        .WithEndpoint(port: 8161, targetPort: 8161, name: "web", scheme: "http");

builder.AddProject<CommunityToolkit_Aspire_Hosting_ActiveMQ_MassTransit>("masstransitExample")
    .WithReference(amq)
    .WaitFor(amq);

builder.Build().Run();
