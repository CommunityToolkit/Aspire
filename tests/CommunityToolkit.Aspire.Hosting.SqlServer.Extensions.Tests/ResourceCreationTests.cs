using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.SqlServer.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public async Task WithDbGateAddsAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();

        var sqlserverResourceBuilder = builder.AddSqlServer("sqlserver")
            .WithDbGate();

        var sqlserverResource = sqlserverResourceBuilder.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("sqlserver-dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);
        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_sqlserver1", item.Key);
                Assert.Equal(sqlserverResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_sqlserver1", item.Key);
                Assert.Equal(sqlserverResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_sqlserver1", item.Key);
                Assert.Equal("sa", item.Value);
            },
            item =>
            {
                Assert.Equal("PASSWORD_sqlserver1", item.Key);
                Assert.Equal(sqlserverResource.PasswordParameter.Value, item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_sqlserver1", item.Key);
                Assert.Equal(sqlserverResource.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_sqlserver1", item.Key);
                Assert.Equal("mssql@dbgate-plugin-mssql", item.Value);
            },
            item =>
            {
                Assert.Equal("CONNECTIONS", item.Key);
                Assert.Equal("sqlserver1", item.Value);
            });
    }

    [Fact]
    public void MultipleWithDbGateCallsAddsOneDbGateResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSqlServer("sqlserver1").WithDbGate();
        builder.AddSqlServer("sqlserver2").WithDbGate();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();
        Assert.NotNull(dbGateResource);

        Assert.Equal("sqlserver1-dbgate", dbGateResource.Name);
    }

    [Fact]
    public void WithDbGateShouldChangeDbGateHostPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlserverResourceBuilder = builder.AddSqlServer("sqlserver")
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
        var sqlserverResourceBuilder = builder.AddSqlServer("sqlserver")
            .WithDbGate(c => c.WithImageTag("manualTag"));
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();
        Assert.NotNull(dbGateResource);

        var containerImageAnnotation = dbGateResource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("manualTag", containerImageAnnotation.Tag);
    }

    [Fact]
    public async Task WithDbGateAddsAnnotationsForMultipleSqlServerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var sqlserverResourceBuilder1 = builder.AddSqlServer("sqlserver1")
            .WithDbGate();

        var sqlserverResource1 = sqlserverResourceBuilder1.Resource;

        var sqlserverResourceBuilder2 = builder.AddSqlServer("sqlserver2")
            .WithDbGate();

        var sqlserverResource2 = sqlserverResourceBuilder2.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("sqlserver1-dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);
        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_sqlserver1", item.Key);
                Assert.Equal(sqlserverResource1.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_sqlserver1", item.Key);
                Assert.Equal(sqlserverResource1.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_sqlserver1", item.Key);
                Assert.Equal("sa", item.Value);
            },
            item =>
            {
                Assert.Equal("PASSWORD_sqlserver1", item.Key);
                Assert.Equal(sqlserverResource1.PasswordParameter.Value, item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_sqlserver1", item.Key);
                Assert.Equal(sqlserverResource1.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_sqlserver1", item.Key);
                Assert.Equal("mssql@dbgate-plugin-mssql", item.Value);
            },
            item =>
            {
                Assert.Equal("LABEL_sqlserver2", item.Key);
                Assert.Equal(sqlserverResource2.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_sqlserver2", item.Key);
                Assert.Equal(sqlserverResource2.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_sqlserver2", item.Key);
                Assert.Equal("sa", item.Value);
            },
            item =>
            {
                Assert.Equal("PASSWORD_sqlserver2", item.Key);
                Assert.Equal(sqlserverResource2.PasswordParameter.Value, item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_sqlserver2", item.Key);
                Assert.Equal(sqlserverResource2.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_sqlserver2", item.Key);
                Assert.Equal("mssql@dbgate-plugin-mssql", item.Value);
            },
            item =>
            {
                Assert.Equal("CONNECTIONS", item.Key);
                Assert.Equal("sqlserver1,sqlserver2", item.Value);
            });
    }
}
