// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr.Tests;

public class AddDaprPubSubTests
{
    [Fact]
    public void DistributedApplicationBuilderCannotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => DistributedApplication.CreateBuilder().AddDaprPubSub(null!));
    }

    [Fact]
    public void ResourceNameCannotBeOmitted()
    {
        string name = "";
        Assert.Throws<ArgumentException>(() => DistributedApplication.CreateBuilder().AddDaprPubSub(name));

        name = " ";
        Assert.Throws<ArgumentException>(() => DistributedApplication.CreateBuilder().AddDaprPubSub(name));

        name = null!;
        Assert.Throws<ArgumentNullException>(() => DistributedApplication.CreateBuilder().AddDaprPubSub(name));
    }

    [Fact]
    public void OptionsConfiguredOnDaprComponent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var name = "pubsub";
        var type = DaprConstants.BuildingBlocks.PubSub;
        var options = new DaprComponentOptions { LocalPath = "path" };

        builder.AddDaprPubSub(name, options);

        var resource = builder.Resources.Single();
        var daprResource = Assert.IsType<DaprComponentResource>(resource);
        Assert.Equal(name, resource.Name);
        Assert.Equal(type, daprResource.Type);
        Assert.Equal(options, daprResource.Options);
    }

    [Fact]
    public void ResourceConfiguredWithHiddenIntialState()
    {
        var builder = DistributedApplication.CreateBuilder();
        var name = "pubsub";

        builder.AddDaprPubSub(name);

        var resource = builder.Resources.Single();
        var daprResource = Assert.IsType<DaprComponentResource>(resource);

        Assert.True(daprResource.TryGetAnnotationsOfType<ResourceSnapshotAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.True(annotation.InitialSnapshot.IsHidden);
    }

    [Fact]
    public void ResourceIncludedInManifest()
    {
        var builder = DistributedApplication.CreateBuilder();
        var name = "pubsub";

        builder.AddDaprPubSub(name);

        var resource = builder.Resources.Single();
        var daprResource = Assert.IsType<DaprComponentResource>(resource);

        Assert.True(daprResource.TryGetAnnotationsOfType<ManifestPublishingCallbackAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.NotNull(annotation.Callback);
    }

    [Fact]
    public void WithMetadata_EndpointReference_AddsMetadataAnnotation()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var redis = builder.AddRedis("redis");
        var endpoint = redis.GetEndpoint("tcp");

        // Act
        builder.AddDaprPubSub("pubsub")
            .WithMetadata("connectionString", endpoint);

        // Assert
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        Assert.True(resource.TryGetAnnotationsOfType<DaprComponentConfigurationAnnotation>(out var annotations));
        Assert.Single(annotations);
    }

    [Fact]
    public void WithMetadata_MultipleEndpointReferences_AddsMultipleMetadata()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var redis1 = builder.AddRedis("redis1");
        var redis2 = builder.AddRedis("redis2");
        var redis1Endpoint = redis1.GetEndpoint("tcp");
        var redis2Endpoint = redis2.GetEndpoint("tcp");

        // Act
        builder.AddDaprPubSub("pubsub")
            .WithMetadata("primaryConnection", redis1Endpoint)
            .WithMetadata("secondaryConnection", redis2Endpoint);

        // Assert
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        Assert.True(resource.TryGetAnnotationsOfType<DaprComponentConfigurationAnnotation>(out var annotations));
        Assert.Equal(2, annotations.Count());
    }

    [Fact]
    public void WithMetadata_EndpointReferenceAndString_BothWork()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var redis = builder.AddRedis("redis");
        var endpoint = redis.GetEndpoint("tcp");

        // Act
        builder.AddDaprPubSub("pubsub")
            .WithMetadata("connectionString", endpoint)
            .WithMetadata("maxRetries", "3");

        // Assert
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        Assert.True(resource.TryGetAnnotationsOfType<DaprComponentConfigurationAnnotation>(out var annotations));
        Assert.Equal(2, annotations.Count());
    }

    [Fact]
    public void WithMetadata_EndpointReferenceAndParameterResource_AddsCorrectMetadata()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("appSecret");
        var redis = builder.AddRedis("redis", port: 6380);
        var endpoint = redis.GetEndpoint("tcp");

        // Act
        builder.AddDaprPubSub("pubsub")
            .WithMetadata("connectionString", endpoint)
            .WithMetadata("secret", parameter.Resource);

        // Assert
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        Assert.True(resource.TryGetAnnotationsOfType<DaprComponentConfigurationAnnotation>(out var configAnnotations));
        Assert.Equal(2, configAnnotations.Count());
    }
}
