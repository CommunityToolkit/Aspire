var builder = DistributedApplication.CreateBuilder(args);

var redpanda = builder.AddRedPanda("redpanda")
    .WithConsole();

// A sample application that produces to and consumes from the Redpanda broker using the
// standard Aspire.Confluent.Kafka client integration.
builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_RedPanda_Consumer>("consumer")
    .WithReference(redpanda)
    .WaitFor(redpanda);

builder.Build().Run();
