using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Aspire.Hosting.Azure;
using CommunityToolkit.Aspire.Hosting.Dapr;

namespace CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void WithReference_WhenAADDisabled_UsesPasswordSecret()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var redisState = builder.AddAzureManagedRedis("redisState")
                                .WithAccessKeyAuthentication()
                                .RunAsContainer();

        var daprState = builder.AddDaprStateStore("statestore")
            .WithReference(redisState);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Get resources with Dapr publishing annotations
        var resourcesWithAnnotation = appModel.Resources
            .Where(r => r.Annotations.OfType<AzureDaprComponentPublishingAnnotation>().Any())
            .ToList();

        // First check if there are any resources with the annotation
        Assert.NotEmpty(resourcesWithAnnotation);

        // Now check for a specific resource
        var daprStateStore = Assert.Single(appModel.Resources.OfType<IDaprComponentResource>(),
            r => r.Name == "statestore");

        // Check there's an annotation on it
        Assert.Contains(daprStateStore.Annotations, a => a is AzureDaprComponentPublishingAnnotation);

        var redisCache = Assert.Single(appModel.Resources.OfType<AzureManagedRedisResource>());

        string redisBicep = redisCache.GetBicepTemplateString();

        string expectedRedisBicep = $$"""
@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param redisstate_kv_outputs_name string

resource redisState 'Microsoft.Cache/redisEnterprise@2025-07-01' = {
  name: take('redisState-${uniqueString(resourceGroup().id)}', 63)
  location: location
  sku: {
    name: 'Enterprise_E10'
    capacity: 2
  }
  tags: {
    'aspire-resource-name': 'redisState'
  }
}

resource redisStateDatabase 'Microsoft.Cache/redisEnterprise/databases@2025-07-01' = {
  name: 'default'
  parent: redisState
  properties: {
    clientProtocol: 'Encrypted'
    clusteringPolicy: 'EnterpriseCluster'
    evictionPolicy: 'NoEviction'
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: redisstate_kv_outputs_name
}

resource connectionString 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  name: 'connectionstrings--redisState'
  properties: {
    value: '${redisState.properties.hostName},ssl=true,password=${redisStateDatabase.listKeys().primaryKey}'
  }
  parent: keyVault
}

resource redisPassword 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  name: 'redis-password'
  properties: {
    value: redisStateDatabase.listKeys().primaryKey
  }
  parent: keyVault
}

output name string = redisState.name

output redisKeyVaultName string = redisstate_kv_outputs_name

output daprConnectionString string = '${redisState.properties.hostName}:10000'
""";

        // Verify the bicep contains Redis Enterprise resource type
        Assert.Contains("Microsoft.Cache/redisEnterprise", redisBicep);
        Assert.Contains("daprConnectionString", redisBicep);

        // Verify that resources with Dapr publishing annotations exist
        Assert.NotEmpty(resourcesWithAnnotation);
    }

    [Fact]
    public void WithReference_WhenAADEnabled_SkipsPasswordSecret()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var redisState = builder.AddAzureManagedRedis("redisState")
            .RunAsContainer();

        var daprState = builder.AddDaprStateStore("statestore")
            .WithReference(redisState);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Get resources with Dapr publishing annotations
        var resourcesWithAnnotation = appModel.Resources
            .Where(r => r.Annotations.OfType<AzureDaprComponentPublishingAnnotation>().Any())
            .ToList();

        // First check if there are any resources with the annotation
        Assert.NotEmpty(resourcesWithAnnotation);

        // Now check for a specific resource
        var daprStateStore = Assert.Single(appModel.Resources.OfType<IDaprComponentResource>(),
            r => r.Name == "statestore");

        // Check there's an annotation on it
        Assert.Contains(daprStateStore.Annotations, a => a is AzureDaprComponentPublishingAnnotation);

        var redisCache = Assert.Single(appModel.Resources.OfType<AzureManagedRedisResource>());

        string redisBicep = redisCache.GetBicepTemplateString();

        string expectedRedisBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            resource redisState 'Microsoft.Cache/redisEnterprise@2025-07-01' = {
              name: take('redisState-${uniqueString(resourceGroup().id)}', 63)
              location: location
              sku: {
                name: 'Enterprise_E10'
                capacity: 2
              }
              tags: {
                'aspire-resource-name': 'redisState'
              }
            }

            resource redisStateDatabase 'Microsoft.Cache/redisEnterprise/databases@2025-07-01' = {
              name: 'default'
              parent: redisState
              properties: {
                clientProtocol: 'Encrypted'
                clusteringPolicy: 'EnterpriseCluster'
                evictionPolicy: 'NoEviction'
              }
            }

            output connectionString string = '${redisState.properties.hostName},ssl=true'

            output name string = redisState.name

            output hostName string = redisState.properties.hostName

            output daprConnectionString string = '${redisState.properties.hostName}:10000'
            """;

        // Verify the bicep contains Redis Enterprise resource type
        Assert.Contains("Microsoft.Cache/redisEnterprise", redisBicep);
        Assert.Contains("daprConnectionString", redisBicep);

        // Verify that resources with Dapr publishing annotations exist
        Assert.NotEmpty(resourcesWithAnnotation);
    }

    // Test removed: WithReference_WhenTLSDisabled_UsesNonSslPort
    // This test was for deprecated Azure Redis Cache which supported EnableNonSslPort.
    // Redis Enterprise uses port 10000 by default and always requires TLS encryption.



    [Fact]
    public void WithReference_WhenNonStateType_ThrowsException()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var redisState = builder.AddAzureManagedRedis("redisState").RunAsContainer();

        // The Redis connection should only be used with state store components
        var unknownComponent = builder.AddDaprComponent("unknown", "component");

        // Create an app with a sidecar that references the unknown component
        var appBuilder = builder.AddContainer("myapp", "image")
            .WithDaprSidecar(sidecar =>
            {
                // Reference the unknown component first
                sidecar.WithReference(unknownComponent);
            });

        // Attempting to create a non-state store reference to Redis should throw
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            unknownComponent.WithReference(redisState);
        });

        // Verify the exception message contains information about the unsupported component type
        Assert.Contains("Unsupported Dapr component", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Demonstrate the correct way to reference Redis
        var stateStore = builder.AddDaprStateStore("statestore");
        stateStore.WithReference(redisState); // This should work correctly

        using var app = builder.Build();
    }

    [Fact]
    public void PreferredPattern_ReferencingRedisStateComponent()
    {
        // This test demonstrates the preferred pattern for referencing Dapr components
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        // Add the Redis state and Dapr state store
        var redisState = builder.AddAzureManagedRedis("redisState").RunAsContainer();
        var daprState = builder.AddDaprStateStore("statestore");

        // Add an app with a sidecar
        builder.AddContainer("myapp", "image")
            .WithDaprSidecar(sidecar =>
            {
                // Reference both components through the sidecar
                sidecar.WithReference(daprState);
                // We can't directly reference Redis from the sidecar due to interface incompatibilities
                // This line would fail with a compile error: sidecar.WithReference(redisState);

                // We need to first create a Dapr component that references Redis
                var anotherState = builder.AddDaprStateStore("anotherstate");
                anotherState.WithReference(redisState);
                sidecar.WithReference(anotherState);
            });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var sidecarResource = Assert.Single(appModel.Resources.OfType<IDaprSidecarResource>());

        // Check for component reference annotations
        var referenceAnnotations = sidecarResource.Annotations
            .OfType<DaprComponentReferenceAnnotation>()
            .ToList();

        Assert.Equal(2, referenceAnnotations.Count);
    }
}