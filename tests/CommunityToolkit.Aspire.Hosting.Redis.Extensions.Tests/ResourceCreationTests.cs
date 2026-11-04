using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
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
            .WithEndpoint("tcp", endpoint => AllocateEndpoint(endpoint, "redis.internal", 27017, tlsEnabled: true))
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
        var password = await redisResource.PasswordParameter!.GetValueAsync(default);
        var expectedRedisUri = $"rediss://:{Uri.EscapeDataString(password!)}@redis.internal:{redisResource.PrimaryEndpoint.TargetPort}";

        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_redis", item.Key);
                Assert.Equal(redisResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("URL_redis", item.Key);
                Assert.Equal(expectedRedisUri, item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_redis", item.Key);
                Assert.Equal("redis@dbgate-plugin-redis", item.Value);
            });
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
        Assert.Single(dbGateResource.Annotations.OfType<CertificateTrustConfigurationCallbackAnnotation>());
    }

    [Theory]
    [InlineData(false, "redis")]
    [InlineData(true, "rediss")]
    public async Task WithDbGateUsesRedisUriExpression(bool tlsEnabled, string expectedScheme)
    {
        var builder = DistributedApplication.CreateBuilder();
        var password = builder.AddParameter("redis-password", "p/a?#%");

        builder.AddRedis("redis", password: password)
            .WithEndpoint("tcp", endpoint => AllocateEndpoint(endpoint, "redis.internal", 27017, tlsEnabled))
            .WithDbGate();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dbGateResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        var envs = await dbGateResource.GetEnvironmentVariablesAsync();

        Assert.Equal($"{expectedScheme}://:p%2Fa%3F%23%25@redis.internal:6379", envs["URL_redis"]);
    }

    [Theory]
    [InlineData(CertificateTrustScope.Append)]
    [InlineData(CertificateTrustScope.Override)]
    public async Task WithDbGateConfiguresNodeCertificateTrust(CertificateTrustScope scope)
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRedis("redis").WithDbGate();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dbGateResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        var callback = Assert.Single(dbGateResource.Annotations.OfType<CertificateTrustConfigurationCallbackAnnotation>());
        var environment = new Dictionary<string, object>();
        var certificateBundlePath = ReferenceExpression.Create($"{Path.Combine(builder.AppHostDirectory, "certs", "bundle.pem")}");
        var context = new CertificateTrustConfigurationCallbackAnnotationContext
        {
            ExecutionContext = builder.ExecutionContext,
            Resource = dbGateResource,
            Arguments = [],
            EnvironmentVariables = environment,
            CertificateBundlePath = certificateBundlePath,
            CertificateDirectoriesPath = ReferenceExpression.Create($"{Path.Combine(builder.AppHostDirectory, "certs")}"),
            Scope = scope,
            CancellationToken = TestContext.Current.CancellationToken
        };

        await callback.Callback(context);

        if (scope == CertificateTrustScope.Append)
        {
            Assert.Same(certificateBundlePath, environment["NODE_EXTRA_CA_CERTS"]);
            Assert.DoesNotContain("NODE_OPTIONS", environment.Keys);
        }
        else
        {
            Assert.Equal("--use-openssl-ca", environment["NODE_OPTIONS"]);
            Assert.DoesNotContain("NODE_EXTRA_CA_CERTS", environment.Keys);
        }
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
            .WithEndpoint("tcp", endpoint => AllocateEndpoint(endpoint, "redis1.internal", 27017, tlsEnabled: true))
            .WithDbGate();

        var redisResource1 = redisResourceBuilder1.Resource;

        var redisResourceBuilder2 = builder.AddRedis("redis2")
            .WithEndpoint("tcp", endpoint => AllocateEndpoint(endpoint, "redis2.internal", 27018, tlsEnabled: true))
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
        var expectedRedis1Uri = $"rediss://:{Uri.EscapeDataString(redis1Password!)}@redis1.internal:{redisResource1.PrimaryEndpoint.TargetPort}";
        var redis2Password = await redisResource2.PasswordParameter!.GetValueAsync(default);
        var expectedRedis2Uri = $"rediss://:{Uri.EscapeDataString(redis2Password!)}@redis2.internal:{redisResource2.PrimaryEndpoint.TargetPort}";

        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_redis1", item.Key);
                Assert.Equal(redisResource1.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("URL_redis1", item.Key);
                Assert.Equal(expectedRedis1Uri, item.Value);
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
                Assert.Equal(expectedRedis2Uri, item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_redis2", item.Key);
                Assert.Equal("redis@dbgate-plugin-redis", item.Value);
            });
    }

    private static void AllocateEndpoint(EndpointAnnotation endpoint, string containerHost, int hostPort, bool tlsEnabled)
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
        endpoint.UriScheme = tlsEnabled ? "rediss" : "redis";
        endpoint.TlsEnabled = tlsEnabled;
    }
}
