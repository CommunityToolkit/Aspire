using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var amq = builder.AddActiveMQ("amq", 
        builder.AddParameter("user", "admin"),
        builder.AddParameter("password", "admin", secret: true), 
        61616, 
        "activemq")
        .PublishAsConnectionString()
        .WithEndpoint(port: 8161, targetPort: 8161, name: "web", scheme: "http");

builder.AddProject<CommunityToolkit_Aspire_Hosting_ActiveMQ_MassTransit>("masstransitExample")
    .WithReference(amq)
    .WaitFor(amq);

builder.Build().Run();
