using Aspire.Hosting;

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
}
