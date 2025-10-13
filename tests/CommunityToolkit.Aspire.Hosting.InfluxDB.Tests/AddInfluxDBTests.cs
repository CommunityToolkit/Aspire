// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Sockets;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.InfluxDB.Tests;

public class AddInfluxDBTests
{
    [Fact]
    public async Task AddInfluxDBContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var influxdb = appBuilder.AddInfluxDB("influxdb");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<InfluxDBResource>());
        Assert.Equal("influxdb", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(8086, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(InfluxDBContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(InfluxDBContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(InfluxDBContainerImageTags.Registry, containerAnnotation.Registry);

        var config = await influxdb.Resource.GetEnvironmentVariableValuesAsync();

        Assert.Collection(config,
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_MODE", env.Key);
                Assert.Equal("setup", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_USERNAME", env.Key);
                Assert.Equal("admin", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_PASSWORD", env.Key);
                Assert.False(string.IsNullOrEmpty(env.Value));
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_ORG", env.Key);
                Assert.Equal("default", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_BUCKET", env.Key);
                Assert.Equal("default", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_ADMIN_TOKEN", env.Key);
                Assert.False(string.IsNullOrEmpty(env.Value));
            });
    }

    [Fact]
    public async Task AddInfluxDBContainerAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var password = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(appBuilder, $"influxdb-password");
        var token = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(appBuilder, $"influxdb-token");

        appBuilder.Configuration["Parameters:influxdb-password"] = await password.GetValueAsync(default);
        appBuilder.Configuration["Parameters:influxdb-token"] = await token.GetValueAsync(default);
        var passwordParameter = appBuilder.AddParameter(password.Name);
        var tokenParameter = appBuilder.AddParameter(token.Name);
        var influxdb = appBuilder.AddInfluxDB("influxdb", userName: null, passwordParameter, tokenParameter);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<InfluxDBResource>());
        Assert.Equal("influxdb", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(8086, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(InfluxDBContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(InfluxDBContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(InfluxDBContainerImageTags.Registry, containerAnnotation.Registry);

        var config = await influxdb.Resource.GetEnvironmentVariableValuesAsync();

        Assert.Collection(config,
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_MODE", env.Key);
                Assert.Equal("setup", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_USERNAME", env.Key);
                Assert.Equal("admin", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_PASSWORD", env.Key);
                Assert.False(string.IsNullOrEmpty(env.Value));
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_ORG", env.Key);
                Assert.Equal("default", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_BUCKET", env.Key);
                Assert.Equal("default", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_ADMIN_TOKEN", env.Key);
                Assert.False(string.IsNullOrEmpty(env.Value));
            });
    }

    [Fact]
    public async Task InfluxDBCreatesConnectionString()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var influxdb = appBuilder
            .AddInfluxDB("influxdb")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27020));

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var connectionStringResource = Assert.Single(appModel.Resources.OfType<InfluxDBResource>()) as IResourceWithConnectionString;
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        Assert.Equal($"Url=http://localhost:27020;Token={await influxdb.Resource.TokenParameter.GetValueAsync(default)}", connectionString);
        Assert.Equal("Url=http://{influxdb.bindings.http.host}:{influxdb.bindings.http.port};Token={influxdb-token.value}", connectionStringResource.ConnectionStringExpression.ValueExpression);
    }
}
