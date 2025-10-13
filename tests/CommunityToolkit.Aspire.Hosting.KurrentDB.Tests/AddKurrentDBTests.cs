// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using System.Net.Sockets;

namespace CommunityToolkit.Aspire.Hosting.KurrentDB.Tests;

public class AddKurrentDBTests
{
    [Fact]
    public async Task AddKurrentDBContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var kurrentdb = appBuilder.AddKurrentDB("kurrentdb");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<KurrentDBResource>());
        Assert.Equal("kurrentdb", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(KurrentDBResource.DefaultHttpPort, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(KurrentDBContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(KurrentDBContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(KurrentDBContainerImageTags.Registry, containerAnnotation.Registry);

        var config = await kurrentdb.Resource.GetEnvironmentVariableValuesAsync();

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
                Assert.Equal($"{KurrentDBResource.DefaultHttpPort}", env.Value);
            },
            ext =>
            {
                Assert.Equal("EVENTSTORE_INSECURE", ext.Key);
                Assert.Equal("true", ext.Value);
            });
    }

    [Fact]
    public async Task KurrentDBCreatesConnectionString()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var kurrentdb = appBuilder
            .AddKurrentDB("kurrentdb")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 22113));

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var connectionStringResource = Assert.Single(appModel.Resources.OfType<KurrentDBResource>()) as IResourceWithConnectionString;
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        Assert.Equal("esdb://localhost:22113?tls=false", connectionString);
        Assert.Equal("esdb://{kurrentdb.bindings.http.host}:{kurrentdb.bindings.http.port}?tls=false", connectionStringResource.ConnectionStringExpression.ValueExpression);
    }
}
