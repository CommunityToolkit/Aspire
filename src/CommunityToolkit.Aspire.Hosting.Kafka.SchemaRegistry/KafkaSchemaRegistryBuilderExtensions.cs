// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Schema Registry services backed by Kafka.
/// </summary>
public static class KafkaSchemaRegistryBuilderExtensions
{
    /// <summary>
    /// Adds a Confluent Schema Registry container connected to an existing Kafka resource.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="name">The resource and connection string name.</param>
    /// <param name="kafka">The Kafka broker that stores schemas.</param>
    /// <param name="port">Optional host port. A port is allocated when omitted.</param>
    /// <returns>The Schema Registry resource builder.</returns>
    /// <remarks>
    /// Configures unauthenticated HTTP and Kafka's plaintext internal listener for local development.
    /// Schemas persist in Kafka's _schemas topic; configure persistence on the Kafka resource.
    /// The resource participates in publish output and does not configure production authentication or TLS.
    /// <code>
    /// var kafka = builder.AddKafka("messaging");
    /// var registry = builder.AddKafkaSchemaRegistry("schema-registry", kafka);
    /// builder.AddProject&lt;Projects.Worker&gt;("worker").WithReference(registry).WaitFor(registry);
    /// </code>
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<KafkaSchemaRegistryResource> AddKafkaSchemaRegistry(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<KafkaServerResource> kafka,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(kafka);

        KafkaSchemaRegistryResource resource = new(name);

        // Use the internal listener so Kafka advertises container-reachable broker addresses.
        // The host-facing listener advertises localhost, which is wrong inside Schema Registry.
        // Confluent maps these environment variables to native configuration properties:
        // https://docs.confluent.io/platform/current/installation/docker/config-reference.html#confluent-schema-registry-configuration
        return builder.AddResource(resource)
            .WithImage(KafkaSchemaRegistryContainerImageTags.Image, KafkaSchemaRegistryContainerImageTags.Tag)
            .WithImageRegistry(KafkaSchemaRegistryContainerImageTags.Registry)
            .WithHttpEndpoint(port: port, targetPort: 8081, name: KafkaSchemaRegistryResource.PrimaryEndpointName)
            .WithEnvironment("SCHEMA_REGISTRY_HOST_NAME", resource.Host)
            .WithEnvironment("SCHEMA_REGISTRY_LISTENERS", ReferenceExpression.Create($"http://0.0.0.0:{resource.PrimaryEndpoint.Property(EndpointProperty.TargetPort)}"))
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS", ReferenceExpression.Create($"PLAINTEXT://{kafka.Resource.InternalEndpoint.Property(EndpointProperty.HostAndPort)}"))
            .WithEnvironment("SCHEMA_REGISTRY_KAFKASTORE_TOPIC_REPLICATION_FACTOR", "1")
            .WithHttpHealthCheck("/subjects")
            .WithIconName("DocumentData")
            .WaitFor(kafka);
    }
}
