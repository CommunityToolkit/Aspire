// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using System.Net.Sockets;

namespace CommunityToolkit.Aspire.Hosting.EventStore.Tests;

public class AddEventStoreTests
{
    [Fact]
    public async Task AddEventStoreContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var eventstore = appBuilder.AddEventStore("eventstore");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<EventStoreResource>());
        Assert.Equal("eventstore", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(EventStoreResource.DefaultHttpPort, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(EventStoreContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(EventStoreContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(EventStoreContainerImageTags.Registry, containerAnnotation.Registry);

        var config = await eventstore.Resource.GetEnvironmentVariableValuesAsync();

        Assert.Collection(config,
            env =>
            {
                Assert.Equal("EVENTSTORE_CLUSTER_SIZE", env.Key);
                Assert.Equal("1", env.Value);
            },
            env =>
            {
                Assert.Equal("EVENTSTORE_RUN_PROJECTIONS", env.Key);
                Assert.Equal("All", env.Value);
            },
            env =>
            {
                Assert.Equal("EVENTSTORE_START_STANDARD_PROJECTIONS", env.Key);
                Assert.Equal("true", env.Value);
            },
            env =>
            {
                Assert.Equal("EVENTSTORE_NODE_PORT", env.Key);
                Assert.Equal($"{EventStoreResource.DefaultHttpPort}", env.Value);
            },
            ext =>
            {
                Assert.Equal("EVENTSTORE_INSECURE", ext.Key);
                Assert.Equal("true", ext.Value);
            });
    }

    [Fact]
    public async Task EventStoreCreatesConnectionString()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var eventstore = appBuilder
            .AddEventStore("eventstore")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 22113));

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var connectionStringResource = Assert.Single(appModel.Resources.OfType<EventStoreResource>()) as IResourceWithConnectionString;
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        Assert.Equal("esdb://localhost:22113?tls=false", connectionString);
        Assert.Equal("esdb://{eventstore.bindings.http.host}:{eventstore.bindings.http.port}?tls=false", connectionStringResource.ConnectionStringExpression.ValueExpression);
    }
}
