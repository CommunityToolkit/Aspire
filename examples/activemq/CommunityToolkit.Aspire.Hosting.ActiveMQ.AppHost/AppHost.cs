using Microsoft.Extensions.DependencyInjection;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("user", "admin");
var password = builder.AddParameter("password", "admin", secret: true);

var amq = builder.AddActiveMQ("amq", username, password, 61616, "activemq", webPort: 8161);

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("masstransitExample", new CommunityToolkit_Aspire_Hosting_ActiveMQ_MassTransit().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(amq)
    .WithHttpHealthCheck(path: "/health")
    .WaitFor(amq);

builder.Build().Run();
