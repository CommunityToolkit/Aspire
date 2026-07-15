using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Dbx.Tests;

public class AddDbxTests
{
    [Fact]
    public void AddDbxContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var dbx = appBuilder.AddDbx();

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbxContainerResource>());
        Assert.Equal("dbx", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(4224, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(DbxContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(DbxContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(DbxContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = dbx.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Fact]
    public void AddDbxContainerWithPort()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var dbx = appBuilder.AddDbx(port: 9090);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbxContainerResource>());
        Assert.Equal("dbx", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(4224, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Equal(9090, primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(DbxContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(DbxContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(DbxContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = dbx.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Fact]
    public void MultipleAddDbxCallsShouldAddOneDbxResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddDbx();
        appBuilder.AddDbx();

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbxContainerResource>());
        Assert.Equal("dbx", containerResource.Name);
    }

    [Fact]
    public void VerifyWithHostPort()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var dbx = appBuilder.AddDbx().WithHostPort(9090);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbxContainerResource>());
        Assert.Equal("dbx", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(4224, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Equal(9090, primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(DbxContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(DbxContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(DbxContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = dbx.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Fact]
    public void WithDbxShouldAddOneDbxResourceForMultipleDatabaseTypes()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddMongoDB("mongodb1")
            .WithDbx();
        
        builder.AddMongoDB("mongodb2")
            .WithDbx();

        builder.AddPostgres("postgres1")
            .WithDbx();

        builder.AddPostgres("postgres2")
            .WithDbx();
        
        builder.AddRedis("redis1")
            .WithDbx();
        
        builder.AddRedis("redis2")
            .WithDbx();

        builder.AddSqlServer("sqlserver1")
            .WithDbx();

        builder.AddSqlServer("sqlserver2")
            .WithDbx();

        builder.AddMySql("mysql1")
            .WithDbx();

        builder.AddMySql("mysql2")
            .WithDbx();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbxResource = appModel.Resources.OfType<DbxContainerResource>().SingleOrDefault();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbxContainerResource>());
        Assert.Equal("dbx", containerResource.Name);
    }

    [Fact]
    [RequiresDocker]
    public async Task AddDbxWithDefaultsAddsUrlAnnotations()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var dbx = builder.AddDbx("dbx");

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dbx.OnResourceEndpointsAllocated((resource, @event, ct) =>
        {
            tcs.SetResult();
            return Task.CompletedTask;
        });

        var app = await builder.BuildAsync();
        await app.StartAsync();
        await tcs.Task;

        var urls = dbx.Resource.Annotations.OfType<ResourceUrlAnnotation>();
        Assert.Single(urls, u => u.DisplayText == "dbx Dashboard");

        await app.StopAsync();
    }
}
