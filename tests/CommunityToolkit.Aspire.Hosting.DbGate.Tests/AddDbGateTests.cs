using System.Net.Sockets;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.DbGate.Tests;
public class AddDbGateTests
{
    [Fact]
    public void AddDbGateContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var dbgate = appBuilder.AddDbGate("dbgate");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        Assert.Equal("dbgate", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(3000, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(DbGateContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(DbGateContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(DbGateContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = dbgate.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Fact]
    public void AddDbGateContainerWithPort()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var dbgate = appBuilder.AddDbGate("dbgate", 9090);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        Assert.Equal("dbgate", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(3000, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Equal(9090, primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(DbGateContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(DbGateContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(DbGateContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = dbgate.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Fact]
    public void MultipleAddDbGateCallsShouldAddOneDbGateResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddDbGate("dbgate1");
        appBuilder.AddDbGate("dbgate2");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        Assert.Equal("dbgate1", containerResource.Name);
    }

    [Fact]
    public void VerifyWithHostPort()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var dbgate = appBuilder.AddDbGate("dbgate").WithHostPort(9090);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        Assert.Equal("dbgate", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(3000, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Equal(9090, primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(DbGateContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(DbGateContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(DbGateContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = dbgate.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VerifyWithData(bool useVolume)
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var dbgate = appBuilder.AddDbGate("dbgate");

        if (useVolume)
        {
            dbgate.WithDataVolume("dbgate-data");
        }
        else
        {
            dbgate.WithDataBindMount("/data/dbgate");
        }

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        Assert.Equal("dbgate", containerResource.Name);

        var mountAnnotations = containerResource.Annotations.OfType<ContainerMountAnnotation>();
        var mountAnnotation = Assert.Single(mountAnnotations);
        Assert.Equal("/root/.dbgate", mountAnnotation.Target);
        if (useVolume)
        {
            Assert.Equal("dbgate-data", mountAnnotation.Source);
            Assert.Equal(ContainerMountType.Volume, mountAnnotation.Type);
            Assert.False(mountAnnotation.IsReadOnly);
        }
        else
        {
            Assert.Equal(Path.GetFullPath("/data/dbgate", appBuilder.AppHostDirectory), mountAnnotation.Source);
            Assert.Equal(ContainerMountType.BindMount, mountAnnotation.Type);
            Assert.False(mountAnnotation.IsReadOnly);
        }
    }

    [Fact]
    public async Task WithDbGateShouldAddAnnotationsForMultipleDatabaseTypes()
    {
        var builder = DistributedApplication.CreateBuilder();

        var mongodbResourceBuilder1 = builder.AddMongoDB("mongodb1")
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27017))
            .WithDbGate();

        var mongodbResource1 = mongodbResourceBuilder1.Resource;

        var mongodbResourceBuilder2 = builder.AddMongoDB("mongodb2")
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27018))
            .WithDbGate();

        var mongodbResource2 = mongodbResourceBuilder2.Resource;

        var postgresResourceBuilder1 = builder.AddPostgres("postgres1")
           .WithDbGate();

        var postgresResource1 = postgresResourceBuilder1.Resource;

        var postgresResourceBuilder2 = builder.AddPostgres("postgres2")
            .WithDbGate();

        var postgresResource2 = postgresResourceBuilder2.Resource;

        var redisResourceBuilder1 = builder.AddRedis("redis1")
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 28017))
            .WithDbGate();

        var redisResource1 = redisResourceBuilder1.Resource;

        var redisResourceBuilder2 = builder.AddRedis("redis2")
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 28018))
            .WithDbGate();

        var redisResource2 = redisResourceBuilder2.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("mongodb1-dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);
        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_mongodb1", item.Key);
                Assert.Equal(mongodbResource1.Name, item.Value);
            },
            async item =>
            {
                Assert.Equal("URL_mongodb1", item.Key);
                Assert.Equal(await mongodbResource1.ConnectionStringExpression.GetValueAsync(default), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_mongodb1", item.Key);
                Assert.Equal("mongo@dbgate-plugin-mongo", item.Value);
            },
            item =>
            {
                Assert.Equal("LABEL_mongodb2", item.Key);
                Assert.Equal(mongodbResource2.Name, item.Value);
            },
            async item =>
            {
                Assert.Equal("URL_mongodb2", item.Key);
                Assert.Equal(await mongodbResource2.ConnectionStringExpression.GetValueAsync(default), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_mongodb2", item.Key);
                Assert.Equal("mongo@dbgate-plugin-mongo", item.Value);
            },
            item =>
            {
                Assert.Equal("CONNECTIONS", item.Key);
                Assert.Equal("mongodb1,mongodb2,postgres1,postgres2,redis1,redis2", item.Value);
            },
            item =>
            {
                Assert.Equal("LABEL_postgres1", item.Key);
                Assert.Equal(postgresResource1.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_postgres1", item.Key);
                Assert.Equal(postgresResource1.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_postgres1", item.Key);
                Assert.Equal("postgres", item.Value);
            },
            item =>
            {
                Assert.Equal("PASSWORD_postgres1", item.Key);
                Assert.Equal(postgresResource1.PasswordParameter.Value, item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_postgres1", item.Key);
                Assert.Equal(postgresResource1.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_postgres1", item.Key);
                Assert.Equal("postgres@dbgate-plugin-postgres", item.Value);
            },
            item =>
            {
                Assert.Equal("LABEL_postgres2", item.Key);
                Assert.Equal(postgresResource2.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_postgres2", item.Key);
                Assert.Equal(postgresResource2.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_postgres2", item.Key);
                Assert.Equal("postgres", item.Value);
            },
            item =>
            {
                Assert.Equal("PASSWORD_postgres2", item.Key);
                Assert.Equal(postgresResource2.PasswordParameter.Value, item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_postgres2", item.Key);
                Assert.Equal(postgresResource2.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_postgres2", item.Key);
                Assert.Equal("postgres@dbgate-plugin-postgres", item.Value);
            },
            item =>
            {
                Assert.Equal("LABEL_redis1", item.Key);
                Assert.Equal(redisResource1.Name, item.Value);
            },
            async item =>
            {
                Assert.Equal("URL_redis1", item.Key);
                Assert.Equal(await redisResource1.ConnectionStringExpression.GetValueAsync(default), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_redis1", item.Key);
                Assert.Equal("redis@dbgate-plugin-redis", item.Value);
            },
            item =>
            {
                Assert.Equal("LABEL_redis2", item.Key);
                Assert.Equal(redisResource2.Name, item.Value);
            },
            async item =>
            {
                Assert.Equal("URL_redis2", item.Key);
                Assert.Equal(await redisResource2.ConnectionStringExpression.GetValueAsync(default), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_redis2", item.Key);
                Assert.Equal("redis@dbgate-plugin-redis", item.Value);
            });
    }

    [Fact]
    public void WithDbGateShouldShouldAddOneDbGateResourceForMultipleDatabaseTypes()
    {
        var builder = DistributedApplication.CreateBuilder();

        var mongodbResourceBuilder1 = builder.AddMongoDB("mongodb1")
            .WithDbGate();

        var mongodbResource1 = mongodbResourceBuilder1.Resource;

        var mongodbResourceBuilder2 = builder.AddMongoDB("mongodb2")
            .WithDbGate();

        var mongodbResource2 = mongodbResourceBuilder2.Resource;

        var postgresResourceBuilder1 = builder.AddPostgres("postgres1")
           .WithDbGate();

        var postgresResource1 = postgresResourceBuilder1.Resource;

        var postgresResourceBuilder2 = builder.AddPostgres("postgres2")
            .WithDbGate();

        var postgresResource2 = postgresResourceBuilder2.Resource;

        var redisResourceBuilder1 = builder.AddRedis("redis1")
            .WithDbGate();

        var redisResource1 = redisResourceBuilder1.Resource;

        var redisResourceBuilder2 = builder.AddRedis("redis2")
            .WithDbGate();

        var redisResource2 = redisResourceBuilder2.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        Assert.Equal("mongodb1-dbgate", containerResource.Name);
    }
}
