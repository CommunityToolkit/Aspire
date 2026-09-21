using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CommunityToolkit.Aspire.Hosting.Kafka.SchemaRegistry.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void AddRegistryValidatesArguments()
    {
        var builder = DistributedApplication.CreateBuilder();
        var kafka = builder.AddKafka("messaging");
        Assert.Throws<ArgumentNullException>("builder", () => KafkaSchemaRegistryBuilderExtensions.AddKafkaSchemaRegistry(null!, "registry", kafka));
        Assert.Throws<ArgumentNullException>("kafka", () => builder.AddKafkaSchemaRegistry("registry", null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddRegistryRejectsMissingName(string? name)
    {
        var builder = DistributedApplication.CreateBuilder();
        var kafka = builder.AddKafka("messaging");
        Assert.ThrowsAny<ArgumentException>(() => builder.AddKafkaSchemaRegistry(name!, kafka));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(18081)]
    public void AddsResourceWithEndpointImageAndReadiness(int? port)
    {
        var builder = DistributedApplication.CreateBuilder();
        var kafka = builder.AddKafka("messaging");
        var registry = builder.AddKafkaSchemaRegistry("registry", kafka, port);
        using var app = builder.Build();
        Assert.Same(registry.Resource, Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<KafkaSchemaRegistryResource>()));
        var endpoint = Assert.Single(registry.Resource.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal("http", endpoint.Name);
        Assert.Equal("http", endpoint.UriScheme);
        Assert.Equal(8081, endpoint.TargetPort);
        Assert.Equal(port, endpoint.Port);
        var image = Assert.Single(registry.Resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("docker.io", image.Registry);
        Assert.Equal("confluentinc/cp-schema-registry", image.Image);
        Assert.Equal("8.2.0", image.Tag);
        var wait = Assert.Single(registry.Resource.Annotations.OfType<WaitAnnotation>());
        Assert.Same(kafka.Resource, wait.Resource);
        Assert.Equal(WaitType.WaitUntilHealthy, wait.WaitType);
        Assert.Contains(app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations,
            registration => registration.Name.StartsWith("registry", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublishConfigurationPreservesReferences()
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", "unused.json"]);
        var kafka = builder.AddKafka("messaging");
        var registry = builder.AddKafkaSchemaRegistry("registry", kafka);
        var consumer = builder.AddContainer("consumer", "example/consumer").WithReference(registry);
        var registryEnvironment = await registry.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);
        var consumerEnvironment = await consumer.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);
        var connectionProperties = ((IResourceWithConnectionString)registry.Resource).GetConnectionProperties()
            .ToDictionary(property => property.Key, property => property.Value.ValueExpression);
        await Verify(new
        {
            ConnectionString = registry.Resource.ConnectionStringExpression.ValueExpression,
            ConnectionProperties = connectionProperties,
            RegistryEnvironment = registryEnvironment,
            ConsumerEnvironment = consumerEnvironment
        });
    }

    [Fact]
    public void MultipleRegistriesKeepTheirBrokerDependencies()
    {
        var builder = DistributedApplication.CreateBuilder();
        var firstKafka = builder.AddKafka("first-kafka");
        var secondKafka = builder.AddKafka("second-kafka");
        var first = builder.AddKafkaSchemaRegistry("first", firstKafka);
        var second = builder.AddKafkaSchemaRegistry("second", secondKafka);
        Assert.NotSame(first.Resource, second.Resource);
        Assert.Same(firstKafka.Resource, Assert.Single(first.Resource.Annotations.OfType<WaitAnnotation>()).Resource);
        Assert.Same(secondKafka.Resource, Assert.Single(second.Resource.Annotations.OfType<WaitAnnotation>()).Resource);
    }
}
