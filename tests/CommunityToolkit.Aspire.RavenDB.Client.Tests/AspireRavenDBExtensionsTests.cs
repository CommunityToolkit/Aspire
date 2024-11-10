using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Raven.Client.Http;

namespace CommunityToolkit.Aspire.RavenDB.Client.Tests;

public class AspireRavenDBExtensionsTests : IClassFixture<RavenDbServerFixture>
{
    private readonly RavenDbServerFixture _serverFixture;

    private const string DefaultConnectionName = "ravendb";
    private string DefaultConnectionString => _serverFixture.ConnectionString ??
                                              throw new InvalidOperationException("The server was not initialized.");

    public AspireRavenDBExtensionsTests(RavenDbServerFixture serverFixture)
    {
        _serverFixture = serverFixture;
    }

    [Fact]
    public void AddKeyedRavenDbClientWithoutDatabaseShouldWork()
    {
        var builder = CreateBuilder();

        var settings = GetDefaultSettings(shouldCreateDatabase: false);
        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, settings);
        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredKeyedService<IDocumentStore>(DefaultConnectionName);

        Assert.Equal(DefaultConnectionString, documentStore.Urls[0]);
        Assert.True(string.IsNullOrEmpty(documentStore.Database));
    }

    [Fact]
    public void AddRavenDbClientWithoutDatabaseShouldWork()
    {
        var builder = CreateBuilder();

        var settings = GetDefaultSettings(shouldCreateDatabase: false);
        builder.AddRavenDBClient(settings);
        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredService<IDocumentStore>();

        Assert.Equal(DefaultConnectionString, documentStore.Urls[0]);
        Assert.True(string.IsNullOrEmpty(documentStore.Database));
    }

    [Fact]
    public void AddKeyedRavenDbClientShouldWork()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var settings = GetDefaultSettings(databaseName);

        var builder = CreateBuilder();
        builder.AddKeyedRavenDBClient(serviceKey:DefaultConnectionName, settings);
        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredKeyedService<IDocumentStore>(DefaultConnectionName);
        using var session = host.Services.GetRequiredKeyedService<IDocumentSession>(DefaultConnectionName);

        Assert.Equal(DefaultConnectionString, documentStore.Urls[0]);
        Assert.Equal(databaseName, documentStore.Database);
        Assert.NotNull(session);
    }

    [Fact]
    public void AddKeyedRavenDbClientShouldWork_asyncSession()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var settings = GetDefaultSettings(databaseName);

        var builder = CreateBuilder();
        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, settings);
        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredKeyedService<IDocumentStore>(DefaultConnectionName);
        using var asyncSession = host.Services.GetRequiredKeyedService<IAsyncDocumentSession>(DefaultConnectionName);

        Assert.Equal(DefaultConnectionString, documentStore.Urls[0]);
        Assert.Equal(databaseName, documentStore.Database);
        Assert.NotNull(asyncSession);
    }

    [Fact]
    public void AddKeyedRavenDbClientAndOpen2SessionShouldNotBeEqual()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var settings = GetDefaultSettings(databaseName);

        var builder = CreateBuilder();
        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, settings);
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
        var settings = GetDefaultSettings(databaseName);

        var builder = CreateBuilder();
        builder.AddRavenDBClient(settings);
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
        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, connectionUrls: new[] { DefaultConnectionString }, databaseName: databaseName);
        using var host = builder.Build();

        Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<IDocumentStore>());

    }

    [Fact]
    public void AddRavenDbClientShouldWork_CreateNewDatabase()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var settings = GetDefaultSettings(databaseName);

        var builder = CreateBuilder();
        builder.AddRavenDBClient(settings);

        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredService<IDocumentStore>();
        using var session = host.Services.GetRequiredService<IDocumentSession>(); //this should  work

        Assert.Equal(DefaultConnectionString, documentStore.Urls[0]);
        Assert.Equal(databaseName, documentStore.Database);

        var product = session.Load<object>("products/77-A");
        Assert.Null(product);

        session.Store(new object(), "products/77-A");
        session.SaveChanges();
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
            new KeyValuePair<string, string?>($"ConnectionStrings:{connectionName1}", DefaultConnectionString),
            new KeyValuePair<string, string?>($"ConnectionStrings:{connectionName2}", DefaultConnectionString)
        ];
        var builder = CreateBuilder(config);

        var settings1 = GetDefaultSettings(databaseName1);
        var settings2 = GetDefaultSettings(databaseName2);

        builder.AddKeyedRavenDBClient(serviceKey: connectionName1, settings1);
        builder.AddKeyedRavenDBClient(serviceKey: connectionName2, settings2);

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

        var product = session1.Load<object>("products/77-A");
        Assert.Null(product);

        product = session2.Load<object>("products/77-A");
        Assert.Null(product);
    }

    [Fact]
    public void AddRavenDbClientShouldWork_Create2NewDatabases()
    {
        var databaseName1 = Guid.NewGuid().ToString("N");
        var databaseName2 = Guid.NewGuid().ToString("N");

        var connectionName1 = Guid.NewGuid().ToString("N");
        var connectionName2 = Guid.NewGuid().ToString("N");

        IEnumerable<KeyValuePair<string, string?>> config =
        [
            new KeyValuePair<string, string?>($"ConnectionStrings:{connectionName1}", DefaultConnectionString),
            new KeyValuePair<string, string?>($"ConnectionStrings:{connectionName2}", DefaultConnectionString)
        ];
        var builder = CreateBuilder(config);

        var settings1 = GetDefaultSettings(databaseName1);
        var settings2 = GetDefaultSettings(databaseName2);

        builder.AddRavenDBClient(settings1);
        builder.AddRavenDBClient(settings2);

        using var host = builder.Build();

        using var documentStore1 = host.Services.GetRequiredService<IDocumentStore>();
        using var documentStore2 = host.Services.GetRequiredService<IDocumentStore>();

        Assert.NotNull(documentStore1);
        Assert.NotNull(documentStore2);

        using var session1 = host.Services.GetRequiredService<IDocumentSession>();
        using var session2 = host.Services.GetRequiredService<IDocumentSession>();

        Assert.Equal(DefaultConnectionString, documentStore1.Urls[0]);
        Assert.Equal(DefaultConnectionString, documentStore2.Urls[0]);

        Assert.True(documentStore1.Database == databaseName1 || documentStore1.Database == databaseName2);
        Assert.True(documentStore2.Database == databaseName1 || documentStore2.Database == databaseName2);

        Assert.NotEqual(session1, session2);
    }

    [Fact]
    public async Task AddRavenDbClient_HealthCheckShouldBeRegisteredWhenEnabled()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var settings = new RavenDBClientSettings(new[] { DefaultConnectionString }, databaseName)
        {
            CreateDatabase = true,
            DisableHealthChecks = false
        };

        var builder = CreateBuilder();
        builder.AddRavenDBClient(settings);
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
        var settings = new RavenDBClientSettings(new[] { DefaultConnectionString }, databaseName)
        {
            CreateDatabase = true,
            DisableHealthChecks = true
        };

        var builder = CreateBuilder();
        builder.AddRavenDBClient(settings);
        using var host = builder.Build();

        var healthCheckService = host.Services.GetService<HealthCheckService>();

        Assert.Null(healthCheckService);

    }

    [Fact]
    public async Task AddKeyedRavenDbClient_HealthCheckShouldBeRegisteredWhenEnabled()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var settings = new RavenDBClientSettings(new[] { DefaultConnectionString }, databaseName)
        {
            CreateDatabase = true,
            DisableHealthChecks = false
        };

        var builder = CreateBuilder();
        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, settings);
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
        var settings = new RavenDBClientSettings(new[] { DefaultConnectionString }, databaseName)
        {
            CreateDatabase = true,
            DisableHealthChecks = true
        };

        var builder = CreateBuilder();
        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, settings);
        using var host = builder.Build();

        var healthCheckService = host.Services.GetService<HealthCheckService>();

        Assert.Null(healthCheckService);

    }

    [Fact]
    public void AddRavenDbClient_ModifyDocumentStore()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var settings = new RavenDBClientSettings(new[] { DefaultConnectionString }, databaseName)
        {
            CreateDatabase = true,
            ModifyDocumentStore = store =>
            {
                store.Conventions.ReadBalanceBehavior = ReadBalanceBehavior.RoundRobin;
                store.Conventions.MaxNumberOfRequestsPerSession = 99;
            }
        };

        var builder = CreateBuilder();
        builder.AddRavenDBClient(settings);
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
        var settings = new RavenDBClientSettings(new[] { DefaultConnectionString }, databaseName)
        {
            CreateDatabase = true,
            ModifyDocumentStore = store =>
            {
                store.Conventions.ReadBalanceBehavior = ReadBalanceBehavior.RoundRobin;
                store.Conventions.MaxNumberOfRequestsPerSession = 99;
            }
        };

        var builder = CreateBuilder();
        builder.AddKeyedRavenDBClient(serviceKey: DefaultConnectionName, settings);
        using var host = builder.Build();

        using var documentStore = host.Services.GetRequiredKeyedService<IDocumentStore>(DefaultConnectionName);

        Assert.NotNull(documentStore);
        Assert.Equal(databaseName, documentStore.Database);
        Assert.Equal(ReadBalanceBehavior.RoundRobin, documentStore.Conventions.ReadBalanceBehavior);
        Assert.Equal(99, documentStore.Conventions.MaxNumberOfRequestsPerSession);
    }

    private RavenDBClientSettings GetDefaultSettings(string? databaseName = null, bool shouldCreateDatabase = true)
    {
        return new RavenDBClientSettings(new[] { DefaultConnectionString }, databaseName)
        {
            CreateDatabase = shouldCreateDatabase
        };
    }

    private HostApplicationBuilder CreateBuilder(IEnumerable<KeyValuePair<string, string?>>? config = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection(config ?? GetDefaultConfiguration());
        return builder;
    }

    private IEnumerable<KeyValuePair<string, string?>> GetDefaultConfiguration() =>
    [
        new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", DefaultConnectionString)
    ];
}
