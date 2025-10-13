// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Neon.Tests;

public class AddNeonTests
{
    [Fact]
    public async Task AddNeonProjectWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var neon = appBuilder.AddNeonProject("neon");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<NeonProjectResource>());
        Assert.Equal("neon", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "tcp");
        Assert.Equal(5432, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("tcp", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("tcp", primaryEndpoint.Transport);
        Assert.Equal("tcp", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(NeonContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(NeonContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(NeonContainerImageTags.Registry, containerAnnotation.Registry);

        var config = await neon.Resource.GetEnvironmentVariableValuesAsync();

        Assert.Equal(2, config.Count());
        Assert.Contains(config, c => c.Key == "POSTGRES_USER");
        Assert.Contains(config, c => c.Key == "POSTGRES_PASSWORD");
    }

    [Fact]
    public async Task AddNeonProjectAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var userName = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(appBuilder, $"userName", special: false);
        var password = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(appBuilder, $"password");

        appBuilder.Configuration["Parameters:userName"] = await userName.GetValueAsync(default);
        appBuilder.Configuration["Parameters:password"] = await password.GetValueAsync(default);
        
        var userNameParameter = appBuilder.AddParameter(userName.Name);
        var passwordParameter = appBuilder.AddParameter(password.Name);
        var neon = appBuilder.AddNeonProject("neon", userNameParameter, passwordParameter);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<NeonProjectResource>());
        Assert.Equal("neon", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "tcp");
        Assert.Equal(5432, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("tcp", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("tcp", primaryEndpoint.Transport);
        Assert.Equal("tcp", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(NeonContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(NeonContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(NeonContainerImageTags.Registry, containerAnnotation.Registry);

        var config = await neon.Resource.GetEnvironmentVariableValuesAsync();

        Assert.Equal(2, config.Count());
        Assert.Contains(config, c => c.Key == "POSTGRES_USER");
        Assert.Contains(config, c => c.Key == "POSTGRES_PASSWORD");
    }

    [Fact]
    public async Task NeonCreatesConnectionString()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var neon = appBuilder
            .AddNeonProject("neon")
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 5432));

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var connectionStringResource = Assert.Single(appModel.Resources.OfType<NeonProjectResource>()) as IResourceWithConnectionString;
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        var expectedUserName = await neon.Resource.UserNameParameter.GetValueAsync(default);
        var expectedPassword = await neon.Resource.PasswordParameter.GetValueAsync(default);

        Assert.Equal($"Host=localhost;Port=5432;Username={expectedUserName};Password={expectedPassword};SSL Mode=Require", connectionString);
    }

    [Fact]
    public void AddDatabaseAddsNeonDatabaseResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var neon = appBuilder.AddNeonProject("neon");

        var db = neon.AddDatabase("mydb", "actual-db-name");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbResource = Assert.Single(appModel.Resources.OfType<NeonDatabaseResource>());
        Assert.Equal("mydb", dbResource.Name);
        Assert.Equal("actual-db-name", dbResource.DatabaseName);
        Assert.Equal(neon.Resource, dbResource.Parent);
    }

    [Fact]
    public async Task NeonDatabaseCreatesConnectionString()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var neon = appBuilder
            .AddNeonProject("neon")
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 5432));

        var db = neon.AddDatabase("mydb");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var connectionStringResource = Assert.Single(appModel.Resources.OfType<NeonDatabaseResource>()) as IResourceWithConnectionString;
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        var expectedUserName = await neon.Resource.UserNameParameter.GetValueAsync(default);
        var expectedPassword = await neon.Resource.PasswordParameter.GetValueAsync(default);

        Assert.Contains("Host=localhost", connectionString);
        Assert.Contains("Port=5432", connectionString);
        Assert.Contains($"Username={expectedUserName}", connectionString);
        Assert.Contains($"Password={expectedPassword}", connectionString);
        Assert.Contains("Database=mydb", connectionString);
        Assert.Contains("SSL Mode=Require", connectionString);
    }
}
