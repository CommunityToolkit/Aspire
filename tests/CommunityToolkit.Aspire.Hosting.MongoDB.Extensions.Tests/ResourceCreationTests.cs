using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.MongoDB.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public async Task WithDbGateAddsAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();

        var mongodbResourceBuilder = builder.AddMongoDB("mongodb")
            .WithEndpoint("tcp", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27017))
            .WithDbGate();

        var mongodbResource = mongodbResourceBuilder.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariablesAsync();

        Assert.NotEmpty(envs);

        var CONNECTIONS = envs["CONNECTIONS"];
        envs.Remove("CONNECTIONS");

        Assert.Equal("mongodb", CONNECTIONS);

        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_mongodb", item.Key);
                Assert.Equal(mongodbResource.Name, item.Value);
            },
            async item =>
            {
                Assert.Equal("URL_mongodb", item.Key);
                Assert.Equal(await mongodbResource.ConnectionStringExpression.GetValueAsync(default), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_mongodb", item.Key);
                Assert.Equal("mongo@dbgate-plugin-mongo", item.Value);
            });
    }

    [Fact]
    public void MultipleWithDbGateCallsAddsOneDbGateResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddMongoDB("mongodb1").WithDbGate();
        builder.AddMongoDB("mongodb2").WithDbGate();

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
        var mongodbResourceBuilder = builder.AddMongoDB("mongodb")
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
        var mongodbResourceBuilder = builder.AddMongoDB("mongodb")
            .WithDbGate(c => c.WithImageTag("manualTag"));
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();
        Assert.NotNull(dbGateResource);

        var containerImageAnnotation = dbGateResource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("manualTag", containerImageAnnotation.Tag);
    }

    [Fact]
    public async Task WithDbGateAddsAnnotationsForMultipleMongoDBResource()
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

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariablesAsync();

        Assert.NotEmpty(envs);

        var CONNECTIONS = envs["CONNECTIONS"];
        envs.Remove("CONNECTIONS");

        Assert.Equal("mongodb1,mongodb2", CONNECTIONS);

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
            });
    }
}
