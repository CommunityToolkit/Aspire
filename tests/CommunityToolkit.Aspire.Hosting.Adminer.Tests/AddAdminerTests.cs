using System.Net.Sockets;
using System.Text.Json;
using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;

namespace CommunityToolkit.Aspire.Hosting.Adminer.Tests;

public class AddAdminerTests
{
    [Fact]
    public void AddAdminerContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var adminer = appBuilder.AddAdminer("adminer");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<AdminerContainerResource>());
        Assert.Equal("adminer", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(8080, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(AdminerContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(AdminerContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(AdminerContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = adminer.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Fact]
    public void AddAdminerContainerWithPort()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var adminer = appBuilder.AddAdminer("adminer", 9090);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<AdminerContainerResource>());
        Assert.Equal("adminer", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(8080, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Equal(9090, primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(AdminerContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(AdminerContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(AdminerContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = adminer.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Fact]
    public void MultipleAddAdminerCallsShouldAddOneAdminerResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddAdminer("adminer1");
        appBuilder.AddAdminer("adminer2");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<AdminerContainerResource>());
        Assert.Equal("adminer1", containerResource.Name);
    }

    [Fact]
    public void VerifyWithHostPort()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var adminer = appBuilder.AddAdminer("adminer").WithHostPort(9090);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<AdminerContainerResource>());
        Assert.Equal("adminer", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(8080, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Equal(9090, primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(AdminerContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(AdminerContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(AdminerContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = adminer.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Fact]
    public async Task WithAdminerShouldAddAnnotationsForMultipleDatabaseTypes()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgresResourceBuilder1 = builder.AddPostgres("postgres1")
           .WithAdminer();

        var postgresResource1 = postgresResourceBuilder1.Resource;

        var postgresResourceBuilder2 = builder.AddPostgres("postgres2")
            .WithAdminer();

        var postgresResource2 = postgresResourceBuilder2.Resource;

        var sqlserverResourceBuilder1 = builder.AddSqlServer("sqlserver1")
            .WithAdminer();

        var sqlserverResource1 = sqlserverResourceBuilder1.Resource;

        var sqlserverResourceBuilder2 = builder.AddSqlServer("sqlserver2")
            .WithAdminer();

        var sqlserverResource2 = sqlserverResourceBuilder2.Resource;


        var mysqlResourceBuilder1 = builder.AddMySql("mysql1")
            .WithAdminer();

        var mysqlResource1 = mysqlResourceBuilder1.Resource;

        var mysqlResourceBuilder2 = builder.AddMySql("mysql2")
            .WithAdminer();

        var mysqlResource2 = mysqlResourceBuilder2.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var adminerContainerResource = appModel.Resources.OfType<AdminerContainerResource>().SingleOrDefault();

        Assert.NotNull(adminerContainerResource);

        Assert.Equal("postgres1-adminer", adminerContainerResource.Name);

        var envs = await adminerContainerResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);

        var servers = new Dictionary<string, AdminerLoginServer>
        {
            {
                "postgres1",
                new AdminerLoginServer
                {
                    Driver = "pgsql",
                    Server = postgresResource1.Name,
                    Password = await postgresResource1.PasswordParameter.GetValueAsync(default),
                    UserName = postgresResource1.UserNameParameter switch {
                        null => "postgres",
                        _ => await postgresResource1.UserNameParameter.GetValueAsync(default)
                    }
                }
            },
            {
                "postgres2",
                new AdminerLoginServer
                {
                    Driver = "pgsql",
                    Server = postgresResource2.Name,
                    Password = await postgresResource2.PasswordParameter.GetValueAsync(default),
                    UserName = postgresResource2.UserNameParameter switch {
                        null => "postgres",
                        _ => await postgresResource2.UserNameParameter.GetValueAsync(default)
                    }
                }
            },
            {
                "sqlserver1",
                new AdminerLoginServer
                {
                    Driver = "mssql",
                    Server = sqlserverResource1.Name,
                    Password = await sqlserverResource1.PasswordParameter.GetValueAsync(default),
                    UserName = "sa"
                }
            },
            {
                "sqlserver2",
                new AdminerLoginServer
                {
                    Driver = "mssql",
                    Server = sqlserverResource2.Name,
                    Password = await sqlserverResource2.PasswordParameter.GetValueAsync(default),
                    UserName = "sa"
                }
            },
            {
                "mysql1",
                new AdminerLoginServer
                {
                    Driver = "server",
                    Server = mysqlResource1.Name,
                    Password = await mysqlResource1.PasswordParameter.GetValueAsync(default),
                    UserName = "root"
                }
            },
            {
                "mysql2",
                new AdminerLoginServer
                {
                    Driver = "server",
                    Server = mysqlResource2.Name,
                    Password = await mysqlResource2.PasswordParameter.GetValueAsync(default),
                    UserName = "root"
                }
            }
        };

        var envValue = JsonSerializer.Serialize(servers);
        var item = Assert.Single(envs);
        Assert.Equal("ADMINER_SERVERS", item.Key);
        Assert.Equal(envValue, item.Value);
    }

    [Fact]
    public void WithAdminerShouldShouldAddOneAdminerResourceForMultipleDatabaseTypes()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPostgres("postgres1")
            .WithAdminer();

        builder.AddPostgres("postgres2")
            .WithAdminer();

        builder.AddSqlServer("sqlserver1")
            .WithAdminer();

        builder.AddSqlServer("sqlserver2")
            .WithAdminer();

        builder.AddMySql("mysql1")
            .WithAdminer();

        builder.AddMySql("mysql2")
            .WithAdminer();


        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var adminerResource = appModel.Resources.OfType<AdminerContainerResource>().SingleOrDefault();

        var containerResource = Assert.Single(appModel.Resources.OfType<AdminerContainerResource>());
        Assert.Equal("postgres1-adminer", containerResource.Name);
    }

    [Fact]
    [RequiresDocker]
    public async Task AddAdminerWithDefaultsAddsUrlAnnotations()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var adminer = builder.AddAdminer("adminer");

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        adminer.OnResourceEndpointsAllocated((resource, @event, ct) =>
        {
            tcs.SetResult();
            return Task.CompletedTask;
        });

        var app = await builder.BuildAsync();
        await app.StartAsync();
        await tcs.Task;

        var urls = adminer.Resource.Annotations.OfType<ResourceUrlAnnotation>();
        Assert.Single(urls, u => u.DisplayText == "Adminer Dashboard");

        await app.StopAsync();
    }
}
