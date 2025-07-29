using Aspire.Hosting;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public async Task WithDbGateAddsAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgresResourceBuilder = builder.AddPostgres("postgres")
            .WithDbGate();

        var postgresResource = postgresResourceBuilder.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("postgres-dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);
        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_postgres1", item.Key);
                Assert.Equal(postgresResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_postgres1", item.Key);
                Assert.Equal(postgresResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_postgres1", item.Key);
                Assert.Equal("postgres", item.Value);
            },
            item =>
            {
                Assert.Equal("PASSWORD_postgres1", item.Key);
                Assert.Equal(postgresResource.PasswordParameter.Value, item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_postgres1", item.Key);
                Assert.Equal(postgresResource.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_postgres1", item.Key);
                Assert.Equal("postgres@dbgate-plugin-postgres", item.Value);
            },
            item =>
            {
                Assert.Equal("CONNECTIONS", item.Key);
                Assert.Equal("postgres1", item.Value);
            });
    }

    [Fact]
    public void MultipleWithDbGateCallsAddsOneDbGateResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddPostgres("postgres1").WithDbGate();
        builder.AddPostgres("postgres2").WithDbGate();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();
        Assert.NotNull(dbGateResource);

        Assert.Equal("postgres1-dbgate", dbGateResource.Name);
    }

    [Fact]
    public void WithDbGateShouldChangeDbGateHostPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgresResourceBuilder = builder.AddPostgres("postgres")
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
        var postgresResourceBuilder = builder.AddPostgres("postgres")
            .WithDbGate(c => c.WithImageTag("manualTag"));
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();
        Assert.NotNull(dbGateResource);

        var containerImageAnnotation = dbGateResource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("manualTag", containerImageAnnotation.Tag);
    }

    [Fact]
    public async Task WithDbGateAddsAnnotationsForMultiplePostgresResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgresResourceBuilder1 = builder.AddPostgres("postgres1")
            .WithDbGate();

        var postgresResource1 = postgresResourceBuilder1.Resource;

        var postgresResourceBuilder2 = builder.AddPostgres("postgres2")
            .WithDbGate();

        var postgresResource2 = postgresResourceBuilder2.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("postgres1-dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);
        Assert.Collection(envs,
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
                Assert.Equal("CONNECTIONS", item.Key);
                Assert.Equal("postgres1,postgres2", item.Value);
            });
    }

    [Fact]
    public async Task WithDbGateAddsAnnotationsForProvidedUsernamePassword()
    {
        var builder = DistributedApplication.CreateBuilder();
        var username = "testuser";
        var password = "Passw0rd!";

        var usernameParamter = builder.AddParameter("postgres-username", username);
        var passwordParamter = builder.AddParameter("postgres-password", password);

        var postgresResourceBuilder = builder.AddPostgres("postgres", userName: usernameParamter, password: passwordParamter)
            .WithDbGate();

        var postgresResource = postgresResourceBuilder.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbGateResource = appModel.Resources.OfType<DbGateContainerResource>().SingleOrDefault();

        Assert.NotNull(dbGateResource);

        Assert.Equal("postgres-dbgate", dbGateResource.Name);

        var envs = await dbGateResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);
        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_postgres1", item.Key);
                Assert.Equal(postgresResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_postgres1", item.Key);
                Assert.Equal(postgresResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_postgres1", item.Key);
                Assert.Equal(username, item.Value);
            },
            item =>
            {
                Assert.Equal("PASSWORD_postgres1", item.Key);
                Assert.Equal(password, item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_postgres1", item.Key);
                Assert.Equal(postgresResource.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_postgres1", item.Key);
                Assert.Equal("postgres@dbgate-plugin-postgres", item.Value);
            },
            item =>
            {
                Assert.Equal("CONNECTIONS", item.Key);
                Assert.Equal("postgres1", item.Value);
            });
    }

    [Fact]
    public async Task WithAdminerAddsAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgresResourceBuilder = builder.AddPostgres("postgres")
            .WithAdminer();

        var postgresResource = postgresResourceBuilder.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var adminerResource = appModel.Resources.OfType<AdminerContainerResource>().SingleOrDefault();

        Assert.NotNull(adminerResource);

        Assert.Equal("postgres-adminer", adminerResource.Name);

        var envs = await adminerResource.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);

        var servers = new Dictionary<string, AdminerLoginServer>
        {
            {
                "postgres",
                new AdminerLoginServer
                {
                    Driver = "pgsql",
                    Server = postgresResource.Name,
                    Password = postgresResource.PasswordParameter.Value,
                    UserName = postgresResource.UserNameParameter?.Value ?? "postgres"
                }
            },
        };

        var envValue = JsonSerializer.Serialize(servers);
        var item = Assert.Single(envs);
        Assert.Equal("ADMINER_SERVERS", item.Key);
        Assert.Equal(envValue, item.Value);
    }

    [Fact]
    public void MultipleWithAdminerCallsAddsOneDbGateResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddPostgres("postgres1").WithAdminer();
        builder.AddPostgres("postgres2").WithAdminer();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var adminerContainer = appModel.Resources.OfType<AdminerContainerResource>().SingleOrDefault();
        Assert.NotNull(adminerContainer);

        Assert.Equal("postgres1-adminer", adminerContainer.Name);
    }

    [Fact]
    public void WithAdminerShouldChangeAdminerHostPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgresResourceBuilder = builder.AddPostgres("postgres")
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
        var postgresResourceBuilder = builder.AddPostgres("postgres")
            .WithAdminer(c => c.WithImageTag("manualTag"));
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var adminerContainer = appModel.Resources.OfType<AdminerContainerResource>().SingleOrDefault();
        Assert.NotNull(adminerContainer);

        var containerImageAnnotation = adminerContainer.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("manualTag", containerImageAnnotation.Tag);
    }

    [Fact]
    public async Task WithAdminerAddsAnnotationsForMultiplePostgresResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var postgresResourceBuilder1 = builder.AddPostgres("postgres1")
            .WithAdminer();

        var postgresResource1 = postgresResourceBuilder1.Resource;

        var postgresResourceBuilder2 = builder.AddPostgres("postgres2")
            .WithDbGate();

        var postgresResource2 = postgresResourceBuilder2.Resource;

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var adminerContainer = appModel.Resources.OfType<AdminerContainerResource>().SingleOrDefault();

        Assert.NotNull(adminerContainer);

        Assert.Equal("postgres1-adminer", adminerContainer.Name);

        var envs = await adminerContainer.GetEnvironmentVariableValuesAsync();

        Assert.NotEmpty(envs);

        var servers = new Dictionary<string, AdminerLoginServer>
        {
            {
                "postgres1",
                new AdminerLoginServer
                {
                    Driver = "pgsql",
                    Server = postgresResource1.Name,
                    Password = postgresResource1.PasswordParameter.Value,
                    UserName = postgresResource1.UserNameParameter?.Value ?? "postgres"
                }
            },
            {
                "postgres2",
                new AdminerLoginServer
                {
                    Driver = "pgsql",
                    Server = postgresResource2.Name,
                    Password = postgresResource2.PasswordParameter.Value,
                    UserName = postgresResource2.UserNameParameter?.Value ?? "postgres"
                }
            }
        };

        var envValue = JsonSerializer.Serialize(servers);
        var item = Assert.Single(envs);
        Assert.Equal("ADMINER_SERVERS", item.Key);
        Assert.Equal(envValue, item.Value);
    }
}
