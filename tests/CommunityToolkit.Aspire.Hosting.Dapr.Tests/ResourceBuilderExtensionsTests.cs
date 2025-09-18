using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using System.Runtime.CompilerServices;
using Xunit;

namespace CommunityToolkit.Aspire.Hosting.Dapr.Tests;
public class ResourceBuilderExtensionsTests
{
    [Fact]
    public void WithMetadataUsingStringAddsDaprComponentConfigurationAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var rb = builder.AddDaprPubSub("pubsub").WithMetadata("name", "value");
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        Assert.Single(resource.Annotations.OfType<DaprComponentConfigurationAnnotation>());
    }

    [Fact]
    public void WithMetadataUsingParameterResourceAddsDaprComponentConfigurationAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("name", string.Empty);
        var rb = builder.AddDaprPubSub("pubsub").WithMetadata("name", parameter.Resource);
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        Assert.Single(resource.Annotations.OfType<DaprComponentConfigurationAnnotation>());
    }

    [Fact]
    public void WithMetadataUsingSecretParameterResourceAddsDaprComponentSecretAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("secret", string.Empty, secret: true);
        var rb = builder.AddDaprPubSub("pubsub").WithMetadata("name", parameter.Resource);
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        Assert.Single(resource.Annotations.OfType<DaprComponentSecretAnnotation>());
    }

    [Fact]
    public void WithMetadataUsingEndpointReferenceAddsValueProviderAnnotation()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var redis = builder.AddRedis("redis");
        var pubsub = builder.AddDaprPubSub("pubsub");
        
        // Act
        pubsub.WithMetadata("redisHost", redis.GetEndpoint("tcp"));
        
        // Assert
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        
        // Check for value provider annotation
        Assert.True(resource.TryGetAnnotationsOfType<DaprComponentValueProviderAnnotation>(out var endpointAnnotations));
        var endpointAnnotation = Assert.Single(endpointAnnotations);
        Assert.Equal("redisHost", endpointAnnotation.MetadataName);
        Assert.Contains("PUBSUB_", endpointAnnotation.EnvironmentVariableName);
        
        // Check for configuration annotation that sets up secretKeyRef
        Assert.True(resource.TryGetAnnotationsOfType<DaprComponentConfigurationAnnotation>(out var configAnnotations));
        Assert.Single(configAnnotations);
    }

    [Fact]
    public async Task WithMetadataUsingValueProviderGeneratesSecretKeyRefInYaml()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var redis = builder.AddRedis("redis");
        var pubsub = builder.AddDaprPubSub("pubsub")
            .WithMetadata("redisHost", redis.GetEndpoint("tcp"));
        
        // Act
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        var schema = new DaprComponentSchema("pubsub", "pubsub.redis");
        
        // Apply configurations
        if (resource.TryGetAnnotationsOfType<DaprComponentConfigurationAnnotation>(out var annotations))
        {
            foreach (var annotation in annotations)
            {
                await annotation.Configure(schema, default);
            }
        }
        
        // Assert
        Assert.Single(schema.Spec.Metadata);
        var metadataItem = Assert.IsType<DaprComponentSpecMetadataSecret>(schema.Spec.Metadata[0]);
        Assert.Equal("redisHost", metadataItem.Name);
        Assert.NotNull(metadataItem.SecretKeyRef);
        Assert.Contains("PUBSUB_", metadataItem.SecretKeyRef.Key);
        
        // Check that YAML contains secretKeyRef
        var yaml = schema.ToString();
        Assert.Contains("secretKeyRef:", yaml);
        Assert.Contains("name: PUBSUB_", yaml);
    }

    [Fact]
    public void WithMetadataAcceptsAnyValueProvider()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var customValueProvider = new TestValueProvider("custom-value");
        var pubsub = builder.AddDaprPubSub("pubsub");

        // Act - This should compile and work because WithMetadata now accepts IValueProvider
        pubsub.WithMetadata("customValue", customValueProvider);

        // Assert
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());

        // Check for value provider annotation
        Assert.True(resource.TryGetAnnotationsOfType<DaprComponentValueProviderAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);
        Assert.Equal("customValue", annotation.MetadataName);
        Assert.Same(customValueProvider, annotation.ValueProvider);
    }

    // Test helper class that implements IValueProvider
    private class TestValueProvider : global::Aspire.Hosting.ApplicationModel.IValueProvider
    {
        private readonly string _value;

        public TestValueProvider(string value)
        {
            _value = value;
        }

        public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(_value);
        }
    }
}
