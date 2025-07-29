// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Utils;
using System.Net.Sockets;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.Tests;

public class AddSurrealServerTests
{
    [Fact]
    public void AddSurrealServerAddsGeneratedPasswordParameterWithUserSecretsParameterDefaultInRunMode()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create();

        var surrealServer = appBuilder.AddSurrealServer("surreal");

        Assert.Equal("Aspire.Hosting.ApplicationModel.UserSecretsParameterDefault", surrealServer.Resource.PasswordParameter.Default?.GetType().FullName);
    }

    [Fact]
    public void AddSurrealServerDoesNotAddGeneratedPasswordParameterWithUserSecretsParameterDefaultInPublishMode()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var surrealServer = appBuilder.AddSurrealServer("surreal");

        Assert.NotEqual("Aspire.Hosting.ApplicationModel.UserSecretsParameterDefault", surrealServer.Resource.PasswordParameter.Default?.GetType().FullName);
    }

    [Fact]
    public async Task AddSurrealServerContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
    
        var surrealServer = appBuilder.AddSurrealServer("surreal");
    
        using var app = appBuilder.Build();
    
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
    
        var containerResource = Assert.Single(appModel.Resources.OfType<SurrealDbServerResource>());
        Assert.Equal("surreal", containerResource.Name);
    
        var endpoint = Assert.Single(containerResource.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal(8000, endpoint.TargetPort);
        Assert.False(endpoint.IsExternal);
        Assert.Equal("tcp", endpoint.Name);
        Assert.Null(endpoint.Port);
        Assert.Equal(ProtocolType.Tcp, endpoint.Protocol);
        Assert.Equal("tcp", endpoint.Transport);
        Assert.Equal("tcp", endpoint.UriScheme);
    
        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(SurrealDbContainerImageTags.Tag + "-dev", containerAnnotation.Tag);
        Assert.Equal(SurrealDbContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(SurrealDbContainerImageTags.Registry, containerAnnotation.Registry);
    
        var config = await surrealServer.Resource.GetEnvironmentVariableValuesAsync();

        Assert.Collection(config,
            env =>
            {
                Assert.Equal("SURREAL_USER", env.Key);
                Assert.NotNull(env.Value);
            },
            env =>
            {
                Assert.Equal("SURREAL_PASS", env.Key);
                Assert.NotNull(env.Value);
                Assert.True(env.Value.Length >= 8);
            });
    }

    [Fact]
    public async Task SurrealServerCreatesConnectionString()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:pass"] = "p@ssw0rd1";

        var pass = appBuilder.AddParameter("pass");
        appBuilder
            .AddSurrealServer("surreal", null, pass)
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 8000));

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var connectionStringResource = Assert.Single(appModel.Resources.OfType<SurrealDbServerResource>());
        var connectionString = await connectionStringResource.GetConnectionStringAsync(default);

        Assert.Equal("Server=ws://localhost:8000/rpc;User=root;Password=p@ssw0rd1", connectionString);
        Assert.Equal("Server=ws://{surreal.bindings.tcp.host}:{surreal.bindings.tcp.port}/rpc;User=root;Password={pass.value}", connectionStringResource.ConnectionStringExpression.ValueExpression);
    }

    [Fact]
    public async Task SurrealServerDatabaseCreatesConnectionString()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:pass"] = "p@ssw0rd1";

        var pass = appBuilder.AddParameter("pass");
        appBuilder
            .AddSurrealServer("surreal", null, pass)
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 8000))
            .AddNamespace("ns", "myns")
            .AddDatabase("db", "mydb");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var surrealResource = Assert.Single(appModel.Resources.OfType<SurrealDbDatabaseResource>());
        var connectionStringResource = (IResourceWithConnectionString)surrealResource;
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        Assert.Equal("Server=ws://localhost:8000/rpc;User=root;Password=p@ssw0rd1;Namespace=myns;Database=mydb", connectionString);
        Assert.Equal("{ns.connectionString};Database=mydb", connectionStringResource.ConnectionStringExpression.ValueExpression);
    }

    [Fact]
    public void ThrowsWithIdenticalChildResourceNames()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var db = builder.AddSurrealServer("surreal1");
        db.AddNamespace("ns").AddDatabase("db");

        Assert.Throws<DistributedApplicationException>(() => db.AddNamespace("ns").AddDatabase("db"));
    }

    [Fact]
    public void ThrowsWithIdenticalChildResourceNamesDifferentParents()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddSurrealServer("surreal1")
            .AddNamespace("ns")
            .AddDatabase("db");

        var db = builder.AddSurrealServer("surreal2");
        Assert.Throws<DistributedApplicationException>(() => db.AddNamespace("ns").AddDatabase("db"));
    }

    [Fact]
    public void CanAddDatabasesWithDifferentNamesOnSingleServerAndNamespace()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var surrealNs = builder.AddSurrealServer("surreal1").AddNamespace("ns");

        var db1 = surrealNs.AddDatabase("db1", "customers1");
        var db2 = surrealNs.AddDatabase("db2", "customers2");

        Assert.Equal("customers1", db1.Resource.DatabaseName);
        Assert.Equal("customers2", db2.Resource.DatabaseName);

        Assert.Equal("{ns.connectionString};Database=customers1", db1.Resource.ConnectionStringExpression.ValueExpression);
        Assert.Equal("{ns.connectionString};Database=customers2", db2.Resource.ConnectionStringExpression.ValueExpression);
    }

    [Fact]
    public void CanAddDatabasesWithTheSameNameOnMultipleServers()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var db1 = builder.AddSurrealServer("surreal1")
            .AddNamespace("ns1", "ns")
            .AddDatabase("db1", "imports");

        var db2 = builder.AddSurrealServer("surreal2")
            .AddNamespace("ns2", "ns")
            .AddDatabase("db2", "imports");

        Assert.Equal("imports", db1.Resource.DatabaseName);
        Assert.Equal("imports", db2.Resource.DatabaseName);

        Assert.Equal("{ns1.connectionString};Database=imports", db1.Resource.ConnectionStringExpression.ValueExpression);
        Assert.Equal("{ns2.connectionString};Database=imports", db2.Resource.ConnectionStringExpression.ValueExpression);
    }
}