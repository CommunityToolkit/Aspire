using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Redis.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public async Task WithDbGateAddsAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redisResourceBuilder = builder.AddRedis("redis")
            .WithEndpoint("tcp", endpoint => AllocateEndpoint(endpoint, "redis.dev.internal", 27017, tlsEnabled: false))
            .WithDbGate();

        var redisResource = redisResourceBuilder.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariablesAsync();

        Assert.NotEmpty(envs);

        var CONNECTIONS = envs["CONNECTIONS"];
        envs.Remove("CONNECTIONS");

        Assert.Equal("redis", CONNECTIONS);

        var password = await redisResource.PasswordParameter!.GetValueAsync(TestContext.Current.CancellationToken);

        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_redis", item.Key);
                Assert.Equal(redisResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("URL_redis", item.Key);
                Assert.Equal($"redis://:{password}@redis.dev.internal:6379", item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_redis", item.Key);
                Assert.Equal("redis@dbgate-plugin-redis", item.Value);
            });

        Assert.Single(dbGateResource.Annotations.OfType<CertificateTrustConfigurationCallbackAnnotation>());
    }

    [Fact]
    public async Task WithDbGateUsesRedisUriExpressionWhenTlsIsDisabled()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redisResource = builder.AddRedis("redis")
            .WithEndpoint("tcp", endpoint => AllocateEndpoint(endpoint, "redis.dev.internal", 27017, tlsEnabled: false))
            .WithDbGate()
            .Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dbGateResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        var envs = await dbGateResource.GetEnvironmentVariablesAsync();
        var password = await redisResource.PasswordParameter!.GetValueAsync(TestContext.Current.CancellationToken);

        Assert.Equal($"redis://:{password}@redis.dev.internal:6379", envs["URL_redis"]);
    }

    [Fact]
    public void MultipleWithDbGateCallsAddsOneDbGateResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRedis("redis1").WithDbGate();
        builder.AddRedis("redis2").WithDbGate();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();
        Assert.NotNull(dbGateResource);

        Assert.Equal("dbgate", dbGateResource.Name);
    }

    [Fact]
    public void WithDbGateShouldChangeDbGateHostPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        var redisResourceBuilder = builder.AddRedis("redis")
            .WithDbGate(c => c.WithHostPort(8068));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();
        Assert.NotNull(dbGateResource);

        var primaryEndpoint = dbGateResource.Annotations.OfType<EndpointAnnotation>().Single();
        Assert.Equal(8068, primaryEndpoint.Port);
    }

    [Fact]
    public void WithDbGateShouldChangeDbGateContainerImageTag()
    {
        var builder = DistributedApplication.CreateBuilder();
        var redisResourceBuilder = builder.AddRedis("redis")
            .WithDbGate(c => c.WithImageTag("manualTag"));
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();
        Assert.NotNull(dbGateResource);

        var containerImageAnnotation = dbGateResource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("manualTag", containerImageAnnotation.Tag);
    }

    [Fact]
    public async Task WithDbGateAddsAnnotationsForMultipleRedisResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redisResourceBuilder1 = builder.AddRedis("redis1")
            .WithEndpoint("tcp", endpoint => AllocateEndpoint(endpoint, "redis1.dev.internal", 27017, tlsEnabled: false))
            .WithDbGate();

        var redisResource1 = redisResourceBuilder1.Resource;

        var redisResourceBuilder2 = builder.AddRedis("redis2")
            .WithEndpoint("tcp", endpoint => AllocateEndpoint(endpoint, "redis2.dev.internal", 27018, tlsEnabled: false))
            .WithDbGate();

        var redisResource2 = redisResourceBuilder2.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariablesAsync();

        Assert.NotEmpty(envs);

        var CONNECTIONS = envs["CONNECTIONS"];
        envs.Remove("CONNECTIONS");

        Assert.Equal("redis1,redis2", CONNECTIONS);

        var redis1Password = await redisResource1.PasswordParameter!.GetValueAsync(default);
        var redis2Password = await redisResource2.PasswordParameter!.GetValueAsync(default);

        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_redis1", item.Key);
                Assert.Equal(redisResource1.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("URL_redis1", item.Key);
                Assert.Equal($"redis://:{redis1Password}@redis1.dev.internal:6379", item.Value);
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
            item =>
            {
                Assert.Equal("URL_redis2", item.Key);
                Assert.Equal($"redis://:{redis2Password}@redis2.dev.internal:6379", item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_redis2", item.Key);
                Assert.Equal("redis@dbgate-plugin-redis", item.Value);
            });
    }

    private static void AllocateEndpoint(
        EndpointAnnotation endpoint,
        string containerHost,
        int hostPort,
        bool tlsEnabled)
    {
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", hostPort);
        endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(
            KnownNetworkIdentifiers.DefaultAspireContainerNetwork,
            new AllocatedEndpoint(
                endpoint,
                containerHost,
                endpoint.TargetPort!.Value,
                EndpointBindingMode.SingleAddress,
                targetPortExpression: null,
                networkId: KnownNetworkIdentifiers.DefaultAspireContainerNetwork));
        endpoint.TlsEnabled = tlsEnabled;
    }
}
