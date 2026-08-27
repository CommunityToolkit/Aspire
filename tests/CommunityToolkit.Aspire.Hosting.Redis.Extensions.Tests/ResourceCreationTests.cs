using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Redis.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public async Task WithDbGateAddsAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redisResourceBuilder = builder.AddRedis("redis")
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27017))
            .WithDbGate();

        var redisResource = redisResourceBuilder.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("dbgate", dbGateResource.Name);

        var envs = await GetEnvironmentVariablesAsync(builder, dbGateResource);

        Assert.NotEmpty(envs);

        var CONNECTIONS = envs["CONNECTIONS"];
        envs.Remove("CONNECTIONS");

        Assert.Equal("redis", CONNECTIONS);

        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_redis", item.Key);
                Assert.Equal(redisResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("URL_redis", item.Key);
                var redisUrl = Assert.IsType<ReferenceExpression>(item.Value);
                Assert.Equal(redisResource.UriExpression.ValueExpression, redisUrl.ValueExpression);
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
            .WithEndpoint("tcp", endpoint =>
            {
                endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 27017);
                endpoint.TlsEnabled = false;
            })
            .WithDbGate()
            .Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dbGateResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        var envs = await GetEnvironmentVariablesAsync(builder, dbGateResource);
        var redisUrl = Assert.IsType<ReferenceExpression>(envs["URL_redis"]);

        Assert.Equal(redisResource.UriExpression.ValueExpression, redisUrl.ValueExpression);
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
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27017))
            .WithDbGate();

        var redisResource1 = redisResourceBuilder1.Resource;

        var redisResourceBuilder2 = builder.AddRedis("redis2")
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27018))
            .WithDbGate();

        var redisResource2 = redisResourceBuilder2.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("dbgate", dbGateResource.Name);

        var envs = await GetEnvironmentVariablesAsync(builder, dbGateResource);

        Assert.NotEmpty(envs);

        var CONNECTIONS = envs["CONNECTIONS"];
        envs.Remove("CONNECTIONS");

        Assert.Equal("redis1,redis2", CONNECTIONS);

        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_redis1", item.Key);
                Assert.Equal(redisResource1.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("URL_redis1", item.Key);
                var redisUrl = Assert.IsType<ReferenceExpression>(item.Value);
                Assert.Equal(redisResource1.UriExpression.ValueExpression, redisUrl.ValueExpression);
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
                var redisUrl = Assert.IsType<ReferenceExpression>(item.Value);
                Assert.Equal(redisResource2.UriExpression.ValueExpression, redisUrl.ValueExpression);
            },
            item =>
            {
                Assert.Equal("ENGINE_redis2", item.Key);
                Assert.Equal("redis@dbgate-plugin-redis", item.Value);
            });
    }

    private static async Task<Dictionary<string, object>> GetEnvironmentVariablesAsync(
        IDistributedApplicationBuilder builder,
        IResource resource)
    {
        Assert.True(resource.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out var annotations));

        var environmentVariables = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, environmentVariables);

        foreach (var annotation in annotations)
        {
            await annotation.Callback(context);
        }

        return environmentVariables;
    }
}
