using Aspire.Hosting;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.MySql.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public async Task WithAdminerAddsAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();

        var mysqlResourceBuilder = builder.AddMySql("mysql")
            .WithAdminer();

        var mysqlResource = mysqlResourceBuilder.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var adminerResource = appModel.Resources.OfType<AdminerContainerResource>().SingleOrDefault();

        Assert.NotNull(adminerResource);

        Assert.Equal("mysql-adminer", adminerResource.Name);

        var envs = await adminerResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);

        var servers = new Dictionary<string, AdminerLoginServer>
        {
            {
                "mysql",
                new AdminerLoginServer
                {
                    Driver = "server",
                    Server = mysqlResource.Name,
                    Password = mysqlResource.PasswordParameter.Value,
                    UserName = "root"
                }
            },
        };

        var envValue = JsonSerializer.Serialize(servers);
        var item = Assert.Single(envs);
        Assert.Equal("ADMINER_SERVERS", item.Key);
        Assert.Equal(envValue, item.Value);
    }

    [Fact]
    public void MultipleWithAdminerCallsAddsOneAdminerResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddMySql("mysql1").WithAdminer();
        builder.AddMySql("mysql2").WithAdminer();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var adminerContainer = appModel.Resources.OfType<AdminerContainerResource>().SingleOrDefault();
        Assert.NotNull(adminerContainer);

        Assert.Equal("mysql1-adminer", adminerContainer.Name);
    }

    [Fact]
    public void WithAdminerShouldChangeAdminerHostPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        var mysqlResourceBuilder = builder.AddMySql("mysql")
            .WithAdminer(c => c.WithHostPort(8068));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var adminerContainer = appModel.Resources.OfType<AdminerContainerResource>().SingleOrDefault();
        Assert.NotNull(adminerContainer);

        var primaryEndpoint = adminerContainer.Annotations.OfType<EndpointAnnotation>().Single();
        Assert.Equal(8068, primaryEndpoint.Port);
    }

    [Fact]
    public void WithAdminerShouldChangeAdminerContainerImageTag()
    {
        var builder = DistributedApplication.CreateBuilder();
        var mysqlResourceBuilder = builder.AddMySql("mysql")
            .WithAdminer(c => c.WithImageTag("manualTag"));
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var adminerContainer = appModel.Resources.OfType<AdminerContainerResource>().SingleOrDefault();
        Assert.NotNull(adminerContainer);

        var containerImageAnnotation = adminerContainer.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("manualTag", containerImageAnnotation.Tag);
    }

    [Fact]
    public async Task WithAdminerAddsAnnotationsForMultipleMySqlResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var mysqlResourceBuilder1 = builder.AddMySql("mysql1")
            .WithAdminer();

        var mysqlResource1 = mysqlResourceBuilder1.Resource;

        var mysqlResourceBuilder2 = builder.AddMySql("mysql2")
            .WithAdminer();

        var mysqlResource2 = mysqlResourceBuilder2.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var adminerContainer = appModel.Resources.OfType<AdminerContainerResource>().SingleOrDefault();

        Assert.NotNull(adminerContainer);

        Assert.Equal("mysql1-adminer", adminerContainer.Name);

        var envs = await adminerContainer.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);

        var servers = new Dictionary<string, AdminerLoginServer>
        {
            {
                "mysql1",
                new AdminerLoginServer
                {
                    Driver = "server",
                    Server = mysqlResource1.Name,
                    Password = mysqlResource1.PasswordParameter.Value,
                    UserName = "root"
                }
            },
            {
                "mysql2",
                new AdminerLoginServer
                {
                    Driver = "server",
                    Server = mysqlResource2.Name,
                    Password = mysqlResource2.PasswordParameter.Value,
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
    public async Task WithDbGateAddsAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();

        var mysqlResourceBuilder = builder.AddMySql("mysql")
            .WithDbGate();

        var mysqlResource = mysqlResourceBuilder.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("mysql-dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);
        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_mysql1", item.Key);
                Assert.Equal(mysqlResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_mysql1", item.Key);
                Assert.Equal(mysqlResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_mysql1", item.Key);
                Assert.Equal("root", item.Value);
            },
            item =>
            {
                Assert.Equal("PASSWORD_mysql1", item.Key);
                Assert.Equal(mysqlResource.PasswordParameter.Value, item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_mysql1", item.Key);
                Assert.Equal(mysqlResource.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_mysql1", item.Key);
                Assert.Equal("mysql@dbgate-plugin-mysql", item.Value);
            },
            item =>
            {
                Assert.Equal("CONNECTIONS", item.Key);
                Assert.Equal("mysql1", item.Value);
            });
    }

    [Fact]
    public void MultipleWithDbGateCallsAddsOneDbGateResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddMySql("mysql1").WithDbGate();
        builder.AddMySql("mysql2").WithDbGate();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();
        Assert.NotNull(dbGateResource);

        Assert.Equal("mysql1-dbgate", dbGateResource.Name);
    }

    [Fact]
    public void WithDbGateShouldChangeDbGateHostPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        var mysqlResourceBuilder = builder.AddMySql("mysql")
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
        var mysqlResourceBuilder = builder.AddMySql("mysql")
            .WithDbGate(c => c.WithImageTag("manualTag"));
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();
        Assert.NotNull(dbGateResource);

        var containerImageAnnotation = dbGateResource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("manualTag", containerImageAnnotation.Tag);
    }

    [Fact]
    public async Task WithDbGateAddsAnnotationsForMultipleMySqlResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var mysqlResourceBuilder1 = builder.AddMySql("mysql1")
            .WithDbGate();

        var mysqlResource1 = mysqlResourceBuilder1.Resource;

        var mysqlResourceBuilder2 = builder.AddMySql("mysql2")
            .WithDbGate();

        var mysqlResource2 = mysqlResourceBuilder2.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("mysql1-dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);
        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_mysql1", item.Key);
                Assert.Equal(mysqlResource1.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_mysql1", item.Key);
                Assert.Equal(mysqlResource1.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_mysql1", item.Key);
                Assert.Equal("root", item.Value);
            },
            item =>
            {
                Assert.Equal("PASSWORD_mysql1", item.Key);
                Assert.Equal(mysqlResource1.PasswordParameter.Value, item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_mysql1", item.Key);
                Assert.Equal(mysqlResource1.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_mysql1", item.Key);
                Assert.Equal("mysql@dbgate-plugin-mysql", item.Value);
            },
            item =>
            {
                Assert.Equal("LABEL_mysql2", item.Key);
                Assert.Equal(mysqlResource2.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_mysql2", item.Key);
                Assert.Equal(mysqlResource2.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_mysql2", item.Key);
                Assert.Equal("root", item.Value);
            },
            item =>
            {
                Assert.Equal("PASSWORD_mysql2", item.Key);
                Assert.Equal(mysqlResource2.PasswordParameter.Value, item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_mysql2", item.Key);
                Assert.Equal(mysqlResource2.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_mysql2", item.Key);
                Assert.Equal("mysql@dbgate-plugin-mysql", item.Value);
            },
            item =>
            {
                Assert.Equal("CONNECTIONS", item.Key);
                Assert.Equal("mysql1,mysql2", item.Value);
            });
    }
}
