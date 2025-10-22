// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Sockets;
using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.GoFeatureFlag.Tests;

public class AddGoFeatureFlagTests
{
    [Fact]
    public void AddGoFeatureFlagContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var goff = appBuilder.AddGoFeatureFlag("goff");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<GoFeatureFlagResource>());
        Assert.Equal("goff", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(1031, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(GoFeatureFlagContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(GoFeatureFlagContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(GoFeatureFlagContainerImageTags.Registry, containerAnnotation.Registry);
    }

    [Fact]
    public void AddGoFeatureFlagContainerAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var goff = appBuilder.AddGoFeatureFlag("goff");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<GoFeatureFlagResource>());
        Assert.Equal("goff", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(1031, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(GoFeatureFlagContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(GoFeatureFlagContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(GoFeatureFlagContainerImageTags.Registry, containerAnnotation.Registry);
    }

    [Fact]
    public async Task GoFeatureFlagCreatesConnectionString()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var goff = appBuilder
            .AddGoFeatureFlag("goff")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27020));

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var connectionStringResource = Assert.Single(appModel.Resources.OfType<GoFeatureFlagResource>()) as IResourceWithConnectionString;
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        Assert.Equal($"Endpoint=http://localhost:27020", connectionString);
        Assert.Equal("Endpoint=http://{goff.bindings.http.host}:{goff.bindings.http.port}", connectionStringResource.ConnectionStringExpression.ValueExpression);
    }
    
    [Theory]
    [InlineData(LogLevel.Debug, "DEBUG")]
    [InlineData(LogLevel.Information, "INFO")]
    [InlineData(LogLevel.Warning, "WARN")]
    [InlineData(LogLevel.Error, "ERROR")]
    public async Task AddSurrealServerContainerWithLogLevel(LogLevel logLevel, string? expected)
    {
        var appBuilder = DistributedApplication.CreateBuilder();
    
        var goff = appBuilder
            .AddGoFeatureFlag("goff")
            .WithLogLevel(logLevel);
    
        using var app = appBuilder.Build();
    
        var config = await goff.Resource.GetEnvironmentVariableValuesAsync();

        bool hasValue = config.TryGetValue("LOGLEVEL", out var value);

        Assert.True(hasValue);
        Assert.Equal(expected, value);
    }
    
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Critical)]
    [InlineData(LogLevel.None)]
    public void AddSurrealServerContainerWithLogLevelThrowsOnUnsupportedLogLevel(LogLevel logLevel)
    {
        var appBuilder = DistributedApplication.CreateBuilder();
    
        var func = () => appBuilder
            .AddGoFeatureFlag("goff")
            .WithLogLevel(logLevel);

        Assert.Throws<ArgumentOutOfRangeException>(func);
    }
}
