using Aspire.Hosting;
using System.Text.Json;
using CommunityToolkit.Aspire.Testing;

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

        Assert.Equal("dbgate", dbGateResource.Name);

        UpdateResourceEndpoint(postgresResource);

        var envs = await dbGateResource.GetEnvironmentVariablesAsync();

        Assert.NotEmpty(envs);

        var CONNECTIONS = envs["CONNECTIONS"];
        envs.Remove("CONNECTIONS");

        Assert.Equal("postgres", CONNECTIONS);

        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_postgres", item.Key);
                Assert.Equal(postgresResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_postgres", item.Key);
                Assert.Equal(postgresResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_postgres", item.Key);
                Assert.Equal("postgres", item.Value);
            },
            async item =>
            {
                Assert.Equal("PASSWORD_postgres", item.Key);
                Assert.Equal(await postgresResource.PasswordParameter.GetValueAsync(default), item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_postgres", item.Key);
                Assert.Equal(postgresResource.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_postgres", item.Key);
                Assert.Equal("postgres@dbgate-plugin-postgres", item.Value);
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

        Assert.Equal("dbgate", dbGateResource.Name);
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

        Assert.Equal("dbgate", dbGateResource.Name);

        UpdateResourceEndpoint(postgresResource1);
        UpdateResourceEndpoint(postgresResource2);

        var envs = await dbGateResource.GetEnvironmentVariablesAsync();

        Assert.NotEmpty(envs);

        var CONNECTIONS = envs["CONNECTIONS"];
        envs.Remove("CONNECTIONS");

        Assert.Equal("postgres1,postgres2", CONNECTIONS);

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
            async item =>
            {
                Assert.Equal("PASSWORD_postgres1", item.Key);
                Assert.Equal(await postgresResource1.PasswordParameter.GetValueAsync(default), item.Value);
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
            async item =>
            {
                Assert.Equal("PASSWORD_postgres2", item.Key);
                Assert.Equal(await postgresResource2.PasswordParameter.GetValueAsync(default), item.Value);
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

        Assert.Equal("dbgate", dbGateResource.Name);

        UpdateResourceEndpoint(postgresResource);

        var envs = await dbGateResource.GetEnvironmentVariablesAsync();

        Assert.NotEmpty(envs);

        var CONNECTIONS = envs["CONNECTIONS"];
        envs.Remove("CONNECTIONS");

        Assert.Equal("postgres", CONNECTIONS);

        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("LABEL_postgres", item.Key);
                Assert.Equal(postgresResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("SERVER_postgres", item.Key);
                Assert.Equal(postgresResource.Name, item.Value);
            },
            item =>
            {
                Assert.Equal("USER_postgres", item.Key);
                Assert.Equal(username, item.Value);
            },
            item =>
            {
                Assert.Equal("PASSWORD_postgres", item.Key);
                Assert.Equal(password, item.Value);
            },
            item =>
            {
                Assert.Equal("PORT_postgres", item.Key);
                Assert.Equal(postgresResource.PrimaryEndpoint.TargetPort.ToString(), item.Value);
            },
            item =>
            {
                Assert.Equal("ENGINE_postgres", item.Key);
                Assert.Equal("postgres@dbgate-plugin-postgres", item.Value);
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

        UpdateResourceEndpoint(postgresResource);

        var envs = await adminerResource.GetEnvironmentVariablesAsync();

        Assert.NotEmpty(envs);

        var servers = new Dictionary<string, AdminerLoginServer>
        {
            {
                "postgres",
                new AdminerLoginServer
                {
                    Driver = "pgsql",
                    Server = postgresResource.Name,
                    Password = await postgresResource.PasswordParameter.GetValueAsync(default),
                    UserName = postgresResource.UserNameParameter is null ? "postgres" : await postgresResource.UserNameParameter.GetValueAsync(default)
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

        UpdateResourceEndpoint(postgresResource1);
        UpdateResourceEndpoint(postgresResource2);

        var envs = await adminerContainer.GetEnvironmentVariablesAsync();

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
                    UserName = postgresResource1.UserNameParameter is null ? "postgres" : await postgresResource1.UserNameParameter.GetValueAsync(default)
                }
            },
            {
                "postgres2",
                new AdminerLoginServer
                {
                    Driver = "pgsql",
                    Server = postgresResource2.Name,
                    Password = await postgresResource2.PasswordParameter.GetValueAsync(default),
                    UserName = postgresResource2.UserNameParameter is null ? "postgres" : await postgresResource2.UserNameParameter.GetValueAsync(default)
                }
            }
        };

        var envValue = JsonSerializer.Serialize(servers);
        var item = Assert.Single(envs);
        Assert.Equal("ADMINER_SERVERS", item.Key);
        Assert.Equal(envValue, item.Value);
    }

    [Fact]
    public async Task WithFlywayMigrationAddsFlywayWithExpectedContainerArgs()
    {
        const string postgresResourceName = "postgres-for-testing";
        const string postgresUsername = "not-default-username";
        const string postgresPassword = "super-secure-password";
        const string postgresDatabaseName = "my-db";

        var builder = DistributedApplication.CreateBuilder();

        var userNameParameter = builder.AddParameter("username-param", postgresUsername);
        var passwordParameter = builder.AddParameter("password-param", postgresPassword);

        var flywayResourceBuilder = builder.AddFlyway("flyway", "./Migrations");
        var postgresBuilder = builder
            .AddPostgres(postgresResourceName, userName: userNameParameter, password: passwordParameter);
        _ = postgresBuilder
            .AddDatabase(postgresDatabaseName)
            .WithFlywayMigration(flywayResourceBuilder);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var retrievedFlywayResource = appModel.Resources.OfType<FlywayResource>().SingleOrDefault();
        Assert.NotNull(retrievedFlywayResource);

        var expectedArgs = new List<string>
        {
            $"-url=jdbc:postgresql://{postgresResourceName}.dev.internal:5432/{postgresDatabaseName}",
            $"-user={postgresUsername}",
            $"-password={postgresPassword}",
            "migrate"
        };

        var endpoint = postgresBuilder.Resource.GetEndpoint("tcp").EndpointAnnotation;
        var ae = new AllocatedEndpoint(endpoint, $"{postgresResourceName}.dev.internal", 10000, EndpointBindingMode.SingleAddress, null, KnownNetworkIdentifiers.DefaultAspireContainerNetwork);
        endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(KnownNetworkIdentifiers.DefaultAspireContainerNetwork, ae);

        var actualArgs = await retrievedFlywayResource.GetArgumentListAsync();
        Assert.Equal(expectedArgs.Count, actualArgs.Count);
        Assert.Collection(
            actualArgs,
            arg => Assert.Equal(expectedArgs[0], arg),
            arg => Assert.Equal(expectedArgs[1], arg),
            arg => Assert.Equal(expectedArgs[2], arg),
            arg => Assert.Equal(expectedArgs[3], arg));
    }

    [Fact]
    public async Task WithFlywayRepairAddsFlywayWithExpectedContainerArgs()
    {
        const string postgresResourceName = "postgres-for-testing";
        const string postgresUsername = "not-default-username";
        const string postgresPassword = "super-secure-password";
        const string postgresDatabaseName = "my-db";

        var builder = DistributedApplication.CreateBuilder();

        var userNameParameter = builder.AddParameter("username-param", postgresUsername);
        var passwordParameter = builder.AddParameter("password-param", postgresPassword);

        var flywayResourceBuilder = builder.AddFlyway("flyway", "./Migrations");
        var postgresBuilder = builder
            .AddPostgres(postgresResourceName, userName: userNameParameter, password: passwordParameter);
        _ = postgresBuilder
            .AddDatabase(postgresDatabaseName)
            .WithFlywayRepair(flywayResourceBuilder);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var retrievedFlywayResource = appModel.Resources.OfType<FlywayResource>().SingleOrDefault();
        Assert.NotNull(retrievedFlywayResource);

        var expectedArgs = new List<string>
        {
            $"-url=jdbc:postgresql://{postgresResourceName}.dev.internal:5432/{postgresDatabaseName}",
            $"-user={postgresUsername}",
            $"-password={postgresPassword}",
            "repair"
        };

        var endpoint = postgresBuilder.Resource.GetEndpoint("tcp").EndpointAnnotation;
        var ae = new AllocatedEndpoint(endpoint, $"{postgresResourceName}.dev.internal", 10000, EndpointBindingMode.SingleAddress, null, KnownNetworkIdentifiers.DefaultAspireContainerNetwork);
        endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(KnownNetworkIdentifiers.DefaultAspireContainerNetwork, ae);

        var actualArgs = await retrievedFlywayResource.GetArgumentListAsync();
        Assert.Equal(expectedArgs.Count, actualArgs.Count);
        Assert.Collection(
            actualArgs,
            arg => Assert.Equal(expectedArgs[0], arg),
            arg => Assert.Equal(expectedArgs[1], arg),
            arg => Assert.Equal(expectedArgs[2], arg),
            arg => Assert.Equal(expectedArgs[3], arg));
    }

    static void UpdateResourceEndpoint(IResourceWithEndpoints resource)
    {
        var endpoint = resource.GetEndpoint("tcp").EndpointAnnotation;
        var ae = new AllocatedEndpoint(endpoint, "storage.dev.internal", 10000, EndpointBindingMode.SingleAddress, null, KnownNetworkIdentifiers.DefaultAspireContainerNetwork);
        endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(KnownNetworkIdentifiers.DefaultAspireContainerNetwork, ae);
    }
}
