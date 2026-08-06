var builder = DistributedApplication.CreateBuilder(args);

var redpanda = builder.AddRedPanda("redpanda")
    .WithConsole()
    .WithKafkaUI();

// A sample application that produces to and consumes from the Redpanda broker using the
// standard Aspire.Confluent.Kafka client integration.
builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_RedPanda_Consumer>("consumer")
    .WithHttpEndpoint()
    .WithReference(redpanda)
    .WaitFor(redpanda)
    // Surface the app's health checks (including the Aspire Kafka producer/consumer connectivity
    // checks) to Aspire, so the resource only reports healthy once it can actually reach the broker.
    .WithHttpHealthCheck("/health");

builder.Build().Run();
