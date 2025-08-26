// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Hosting.Dapr.Tests;

public class ComponentSchemaTests
{

    [Fact]
    public void ComponentFromStringDeserializesCorrectly()
    {
        string componentString = $"""
                                apiVersion: dapr.io/v1alpha1
                                kind: Component
                                metadata:
                                  name: pubsub
                                  namespace: default
                                spec:
                                  type: pubsub.rabbitmq
                                  version: v1
                                  metadata:
                                  - name: protocol
                                    value: amqp
                                  - name: hostname
                                    value: localhost
                                  - name: username
                                    value: guest
                                  - name: password
                                    value: guest
                                """;

        DaprComponentSchema componentSchema = DaprComponentSchema.FromYaml(componentString);

        Assert.Equal("dapr.io/v1alpha1", componentSchema.ApiVersion);
        Assert.Equal("Component", componentSchema.Kind);
        Assert.Equal("pubsub", componentSchema.Metadata.Name);
        Assert.Equal("default", componentSchema.Metadata.Namespace);
        Assert.Equal("pubsub.rabbitmq", componentSchema.Spec.Type);
        Assert.Equal("v1", componentSchema.Spec.Version);
        Assert.Equal(4, componentSchema.Spec.Metadata.Count);
    }

    [Fact]
    public void ComponentToStringSerializesCorrectly()
    {
        DaprComponentSchema componentSchema = new("pubsub", "pubsub.rabbitmq");

        componentSchema.Spec.Metadata.AddRange([
            new DaprComponentSpecMetadataValue{ Name = "protocol", Value = "amqp" },
            new DaprComponentSpecMetadataValue{ Name = "hostname", Value = "localhost" },
            new DaprComponentSpecMetadataValue{ Name = "username", Value = "guest" },
            new DaprComponentSpecMetadataValue{ Name = "password", Value = "guest" }
        ]);

        string componentString = componentSchema.ToString();

        Assert.Contains("apiVersion: dapr.io/v1alpha1", componentString);
        Assert.Contains("kind: Component", componentString);
        Assert.Contains("metadata:", componentString);
        Assert.Contains("name: pubsub", componentString);
        Assert.Contains("spec:", componentString);
        Assert.Contains("type: pubsub.rabbitmq", componentString);
        Assert.Contains("version: v1", componentString);
        Assert.Contains("metadata:", componentString);
        Assert.Contains("name: protocol", componentString);
        Assert.Contains("value: amqp", componentString);
        Assert.Contains("name: hostname", componentString);
        Assert.Contains("value: localhost", componentString);
        Assert.Contains("name: username", componentString);
        Assert.Contains("value: guest", componentString);
        Assert.Contains("name: password", componentString);
        Assert.Contains("value: guest", componentString);
    }

    [Fact]
    public void ComponentSchemaDeserializesSecretKeyRefsCorrectly()
    {
        string componentString = $"""
                                apiVersion: dapr.io/v1alpha1
                                kind: Component
                                metadata:
                                  name: pubsub
                                  namespace: default
                                spec:
                                  type: pubsub.rabbitmq
                                  version: v1
                                  metadata:
                                  - name: protocol
                                    value: amqp
                                  - name: hostname
                                    value: localhost
                                  - name: username
                                    value: guest
                                  - name: password
                                    secretKeyRef:
                                      name: password
                                      key: password
                                """;
        DaprComponentSchema componentSchema = DaprComponentSchema.FromYaml(componentString);
        Assert.Equal("dapr.io/v1alpha1", componentSchema.ApiVersion);
        Assert.Equal("Component", componentSchema.Kind);
        Assert.Equal("pubsub", componentSchema.Metadata.Name);
        Assert.Equal("default", componentSchema.Metadata.Namespace);
        Assert.Equal("pubsub.rabbitmq", componentSchema.Spec.Type);
        Assert.Equal("v1", componentSchema.Spec.Version);
        Assert.Equal(4, componentSchema.Spec.Metadata.Count);

        var secret = Assert.IsAssignableFrom<DaprComponentSpecMetadataSecret>(componentSchema.Spec.Metadata[3]);
        Assert.Equal("password", secret.SecretKeyRef.Name);
        Assert.Equal("password", secret.SecretKeyRef.Key);
    }

    [Fact]
    public async Task ComponentSchemaResolvesAsyncValuesCorrectly()
    {
        // Create a test value provider
        var testValueProvider = new TestValueProvider("resolved-value");
        
        // Create a component schema with async value provider
        DaprComponentSchema componentSchema = new("testComponent", "state.redis");
        componentSchema.Spec.Metadata.Add(new DaprComponentSpecMetadataValueProvider
        {
            Name = "redisHost",
            ValueProvider = testValueProvider
        });
        
        // Before resolution, Value should be null
        var metadataItem = componentSchema.Spec.Metadata.First() as DaprComponentSpecMetadataValueProvider;
        Assert.NotNull(metadataItem);
        Assert.Null(metadataItem.Value);
        
        // Resolve all async values
        await componentSchema.ResolveAllValuesAsync();
        
        // After resolution, Value should be populated
        Assert.Equal("resolved-value", metadataItem.Value);
        
        // The serialized YAML should contain the resolved value
        string yaml = componentSchema.ToString();
        Assert.Contains("name: redisHost", yaml);
        Assert.Contains("value: resolved-value", yaml);
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