var builder = DistributedApplication.CreateBuilder(args);

var kafka = builder.AddKafka("messaging");
var registry = builder.AddKafkaSchemaRegistry("schema-registry", kafka);

builder.AddProject<Projects.CommunityToolkit_Aspire_Kafka_Dekaf_ApiService>("api")
    .WithHttpEndpoint()
    .WithHttpHealthCheck("/health")
    .WithReference(kafka)
    .WithReference(registry)
    .WaitFor(kafka)
    .WaitFor(registry);

builder.Build().Run();
