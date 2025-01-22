using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Raven.Client.Http;

namespace CommunityToolkit.Aspire.RavenDB.Client.Tests;

public class AspireRavenDBExtensionsTests(RavenDbServerFixture serverFixture) : IClassFixture<RavenDbServerFixture>
{
    private const string DefaultConnectionName = "ravendb";
    private string DefaultConnectionString => serverFixture.ConnectionString ??
                                              throw new InvalidOperationException("The server was not initialized.");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddKeyedRavenDbClient_ReadsFromConnectionStringsCorrectly(bool shouldCreateDatabase)
    {
        var builder = CreateBuilder();

        string? databaseName = null;
        Action<RavenDBClientSettings>? configSettings = null;
        if (shouldCreateDatabase)
        {
            databaseName = Guid.NewGuid().ToString("N");
            configSettings = clientSettings =>
            {
                clientSettings.DatabaseName = databaseName;
                clientSettings.CreateDatabase = true;
            };
        }

        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, connectionName: DefaultConnectionName, configureSettings: configSettings);
        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredKeyedService<IDocumentStore>(DefaultConnectionName);
        Assert.Equal(DefaultConnectionString, documentStore.Urls[0]);

        if (shouldCreateDatabase)
        {
            Assert.Equal(databaseName, documentStore.Database);

            using var session = host.Services.GetRequiredKeyedService<IDocumentSession>(DefaultConnectionName);
            Assert.NotNull(session);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddRavenDbClientWithSettingsShouldWork(bool shouldCreateDatabase)
    {
        var builder = CreateBuilder();

        string? databaseName = null;
        if (shouldCreateDatabase)
            databaseName = Guid.NewGuid().ToString("N");

        var settings = GetDefaultSettings(databaseName, shouldCreateDatabase);

        builder.AddRavenDBClient(settings: settings);
        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredService<IDocumentStore>();
        Assert.Equal(DefaultConnectionString, documentStore.Urls[0]);

        if (shouldCreateDatabase)
        {
            Assert.Equal(databaseName, documentStore.Database);

            using var session = host.Services.GetRequiredService<IDocumentSession>();
            Assert.NotNull(session);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddKeyedRavenDbClientWithSettingsShouldWork(bool shouldCreateDatabase)
    {
        var builder = CreateBuilder();

        string? databaseName = null;
        if (shouldCreateDatabase)
            databaseName = Guid.NewGuid().ToString("N");

        var settings = GetDefaultSettings(databaseName, shouldCreateDatabase);

        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, settings: settings);
        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredKeyedService<IDocumentStore>(DefaultConnectionName);
        Assert.Equal(DefaultConnectionString, documentStore.Urls[0]);

        if (shouldCreateDatabase)
        {
            Assert.Equal(databaseName, documentStore.Database);

            using var session = host.Services.GetRequiredKeyedService<IDocumentSession>(DefaultConnectionName);
            using var asyncSession = host.Services.GetRequiredKeyedService<IAsyncDocumentSession>(DefaultConnectionName);
            Assert.NotNull(session);
            Assert.NotNull(asyncSession);
        }
    }

    [Fact]
    public void AddKeyedRavenDbClientAndOpen2SessionShouldNotBeEqual()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();
        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
            clientSettings.DatabaseName = databaseName;
            clientSettings.CreateDatabase = true;
        });
        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredKeyedService<IDocumentStore>(DefaultConnectionName);

        using var session1 = host.Services.GetRequiredKeyedService<IDocumentSession>(DefaultConnectionName);
        using var session2 = host.Services.GetRequiredKeyedService<IDocumentSession>(DefaultConnectionName);

        Assert.NotEqual(session1, session2);
    }

    [Fact]
    public void AddRavenDbClientAndOpen2SessionShouldNotBeEqual()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();
        builder.AddRavenDBClient(connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
            clientSettings.DatabaseName = databaseName;
            clientSettings.CreateDatabase = true;
        });
        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredService<IDocumentStore>();

        using var session1 = host.Services.GetRequiredService<IDocumentSession>();
        using var session2 = host.Services.GetRequiredService<IDocumentSession>();

        Assert.NotEqual(session1, session2);
    }

    [Fact]
    public void AddKeyedRavenDbClientThenGetRequiredServiceShouldThrow()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();
        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, connectionName: DefaultConnectionName, configureSettings: settings => settings.DatabaseName = databaseName);
        using var host = builder.Build();

        Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<IDocumentStore>());
    }

    [Fact]
    public void AddKeyedRavenDbClientShouldWork_Create2NewDatabases()
    {
        var databaseName1 = Guid.NewGuid().ToString("N");
        var databaseName2 = Guid.NewGuid().ToString("N");

        var connectionName1 = Guid.NewGuid().ToString("N");
        var connectionName2 = Guid.NewGuid().ToString("N");

        IEnumerable<KeyValuePair<string, string?>> config =
        [
            new KeyValuePair<string, string?>($"ConnectionStrings:{connectionName1}", $"URL={DefaultConnectionString}"),
            new KeyValuePair<string, string?>($"ConnectionStrings:{connectionName2}", $"URL={DefaultConnectionString}")
        ];
        var builder = CreateBuilder(config);

        builder.AddKeyedRavenDBClient(serviceKey: connectionName1, connectionName: connectionName1, configureSettings: clientSettings =>
        {
            clientSettings.DatabaseName = databaseName1;
            clientSettings.CreateDatabase = true;
        });
        builder.AddKeyedRavenDBClient(serviceKey: connectionName2, connectionName: connectionName2, configureSettings: clientSettings =>
        {
            clientSettings.DatabaseName = databaseName2;
            clientSettings.CreateDatabase = true;
        });

        using var host = builder.Build();

        using var documentStore1 = host.Services.GetRequiredKeyedService<IDocumentStore>(connectionName1);
        using var documentStore2 = host.Services.GetRequiredKeyedService<IDocumentStore>(connectionName2);

        Assert.NotNull(documentStore1);
        Assert.NotNull(documentStore2);

        using var session1 = host.Services.GetRequiredKeyedService<IDocumentSession>(connectionName1);
        using var session2 = host.Services.GetRequiredKeyedService<IDocumentSession>(connectionName2);

        Assert.Equal(DefaultConnectionString, documentStore1.Urls[0]);
        Assert.Equal(DefaultConnectionString, documentStore2.Urls[0]);

        Assert.Equal(databaseName1, documentStore1.Database);
        Assert.Equal(databaseName2, documentStore2.Database);

        Assert.NotEqual(session1, session2);
    }

    [Fact]
    public async Task AddRavenDbClient_HealthCheckShouldBeRegisteredWhenEnabled()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();

        builder.AddRavenDBClient(connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
            clientSettings.CreateDatabase = true;
            clientSettings.DatabaseName = databaseName;
        });

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        var healthCheckReport = await healthCheckService.CheckHealthAsync();

        var healthCheckName = "RavenDB.Client";

        Assert.Contains(healthCheckReport.Entries, x => x.Key == healthCheckName);
    }

    [Fact]
    public void AddRavenDbClient_HealthCheckShouldNotBeRegisteredWhenDisabled()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();

        builder.AddRavenDBClient(connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
            clientSettings.CreateDatabase = true;
            clientSettings.DatabaseName = databaseName;
            clientSettings.DisableHealthChecks = true;
        });

        using var host = builder.Build();

        var healthCheckService = host.Services.GetService<HealthCheckService>();

        Assert.Null(healthCheckService);
    }

    [Fact]
    public async Task AddKeyedRavenDbClient_HealthCheckShouldBeRegisteredWhenEnabled()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();

        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
            clientSettings.CreateDatabase = true;
            clientSettings.DatabaseName = databaseName;
        });

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        var healthCheckReport = await healthCheckService.CheckHealthAsync();

        var healthCheckName = $"RavenDB.Client_{DefaultConnectionName}";

        Assert.Contains(healthCheckReport.Entries, x => x.Key == healthCheckName);
    }

    [Fact]
    public void AddKeyedRavenDbClient_HealthCheckShouldNotBeRegisteredWhenDisabled()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();

        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
            clientSettings.CreateDatabase = true;
            clientSettings.DatabaseName = databaseName;
            clientSettings.DisableHealthChecks = true;
        });

        using var host = builder.Build();

        var healthCheckService = host.Services.GetService<HealthCheckService>();

        Assert.Null(healthCheckService);
    }

    [Fact]
    public void AddRavenDbClient_ModifyDocumentStore()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();

        builder.AddRavenDBClient(connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
            clientSettings.CreateDatabase = true;
            clientSettings.DatabaseName = databaseName;
            clientSettings.ModifyDocumentStore = store =>
            {
                store.Conventions.ReadBalanceBehavior = ReadBalanceBehavior.RoundRobin;
                store.Conventions.MaxNumberOfRequestsPerSession = 99;
            };
        });

        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredService<IDocumentStore>();

        Assert.NotNull(documentStore);
        Assert.Equal(databaseName, documentStore.Database);
        Assert.Equal(ReadBalanceBehavior.RoundRobin, documentStore.Conventions.ReadBalanceBehavior);
        Assert.Equal(99, documentStore.Conventions.MaxNumberOfRequestsPerSession);
    }

    [Fact]
    public void AddKeyedRavenDbClient_ModifyDocumentStore()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();

        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
            clientSettings.CreateDatabase = true;
            clientSettings.DatabaseName = databaseName;
            clientSettings.ModifyDocumentStore = store =>
            {
                store.Conventions.ReadBalanceBehavior = ReadBalanceBehavior.RoundRobin;
                store.Conventions.MaxNumberOfRequestsPerSession = 99;
            };
        });

        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredKeyedService<IDocumentStore>(DefaultConnectionName);

        Assert.NotNull(documentStore);
        Assert.Equal(databaseName, documentStore.Database);
        Assert.Equal(ReadBalanceBehavior.RoundRobin, documentStore.Conventions.ReadBalanceBehavior);
        Assert.Equal(99, documentStore.Conventions.MaxNumberOfRequestsPerSession);
    }

    private HostApplicationBuilder CreateBuilder(IEnumerable<KeyValuePair<string, string?>>? config = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection(config ?? GetDefaultConfiguration());
        return builder;
    }

    private IEnumerable<KeyValuePair<string, string?>> GetDefaultConfiguration() =>
    [
        new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", $"URL={DefaultConnectionString}")
    ];

    private RavenDBClientSettings GetDefaultSettings(string? databaseName = null, bool shouldCreateDatabase = true)
    {
        return new RavenDBClientSettings
        {
            Urls = new[] { DefaultConnectionString },
            DatabaseName = databaseName,
            CreateDatabase = shouldCreateDatabase
        };
    }
}
