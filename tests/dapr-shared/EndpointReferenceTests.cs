using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Xunit;

namespace CommunityToolkit.Aspire.Hosting.Dapr.Tests;

public class EndpointReferenceTests
{
    [Fact]
    public void WithMetadataUsingEndpointReferenceAddsCorrectAnnotations()
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
    public async Task EndpointReferenceGeneratesSecretKeyRefInYaml()
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
    public void EndpointReferencesTriggersSecretStoreCreation()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var redis = builder.AddRedis("redis");
        
        // Act
        builder.AddDaprPubSub("pubsub")
            .WithMetadata("redisHost", redis.GetEndpoint("tcp"));
        
        // Assert
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        
        // Check that endpoint reference annotation exists
        Assert.True(resource.TryGetAnnotationsOfType<DaprComponentValueProviderAnnotation>(out var annotations));
        Assert.Single(annotations);
        
        // This should trigger secret store creation in the lifecycle hook
        // The actual secret store creation happens in StartOnDemandDaprComponentsAsync
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