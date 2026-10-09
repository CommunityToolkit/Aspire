using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ChromaDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Chroma.Tests;

public class ChromaClientExtensionsTests
{
    private const string DefaultConnectionName = "chroma";
    private const string DefaultConnectionString = "http://localhost:8000";

    [Fact]
    public void AddChromaClient_ShouldRegisterClient()
    {
        var builder = CreateBuilder();

        builder.AddChromaClient(DefaultConnectionName);

        using var host = builder.Build();

        var client = host.Services.GetService<ChromaClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void AddKeyedChromaClient_ShouldRegisterKeyedClient()
    {
        var builder = CreateBuilder();

        builder.AddKeyedChromaClient(DefaultConnectionName);

        using var host = builder.Build();

        var client = host.Services.GetKeyedService<ChromaClient>(DefaultConnectionName);
        Assert.NotNull(client);
    }

    [Fact]
    public async Task AddChromaClient_HealthCheckShouldBeRegistered()
    {
        var builder = CreateBuilder();

        builder.AddChromaClient(DefaultConnectionName);

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var healthCheckReport = await healthCheckService.CheckHealthAsync();
        Assert.Contains(healthCheckReport.Entries, x => x.Key == "Chroma");
    }

    [Fact]
    public async Task AddKeyedChromaClient_HealthCheckShouldBeRegisteredWithSuffix()
    {
        var builder = CreateBuilder();

        builder.AddKeyedChromaClient(DefaultConnectionName);

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var healthCheckReport = await healthCheckService.CheckHealthAsync();
        Assert.Contains(healthCheckReport.Entries, x => x.Key == $"Chroma_{DefaultConnectionName}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddChromaClient_WorksWithoutAnHttpClientFactoryRegisteredByTheApp(bool useKeyed)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", DefaultConnectionString)
        ]);

        if (useKeyed)
        {
            builder.AddKeyedChromaClient(DefaultConnectionName);
        }
        else
        {
            builder.AddChromaClient(DefaultConnectionName);
        }

        using var host = builder.Build();

        var client = useKeyed
            ? host.Services.GetKeyedService<ChromaClient>(DefaultConnectionName)
            : host.Services.GetService<ChromaClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public async Task AddChromaClient_HealthCheckHonorsTheTimeout()
    {
        // A server that accepts the connection and never answers.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var accept = listener.AcceptTcpClientAsync();
        var endpoint = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", endpoint)
        ]);
        builder.AddChromaClient(DefaultConnectionName, settings => settings.HealthCheckTimeout = 500);

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var stopwatch = Stopwatch.StartNew();
        var report = await healthCheckService.CheckHealthAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.Equal(HealthStatus.Unhealthy, report.Entries["Chroma"].Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"The health check took {stopwatch.Elapsed}.");
    }

    // The settings are read from Aspire:Chroma:Client, and from Aspire:Chroma:Client:{name} for a keyed client,
    // like the other client integrations.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettingsAreReadFromTheConfiguration(bool useKeyed)
    {
        var builder = CreateBuilder();
        var section = useKeyed ? $"Aspire:Chroma:Client:{DefaultConnectionName}" : "Aspire:Chroma:Client";
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"{section}:DisableHealthChecks", "true")
        ]);
        builder.Services.AddHealthChecks();

        if (useKeyed)
        {
            builder.AddKeyedChromaClient(DefaultConnectionName);
        }
        else
        {
            builder.AddChromaClient(DefaultConnectionName);
        }

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(TestContext.Current.CancellationToken);
        Assert.Empty(report.Entries);
    }

    // A connection string like the one of Chroma Cloud: the requests go to its tenant and database, with the token in X-Chroma-Token.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConnectionStringWithTokenTenantAndDatabase(bool useKeyed)
    {
        List<HttpRequestMessage> requests = [];
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", "Endpoint=https://chroma.example;Token=secret;Tenant=my_tenant;Database=my_database")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedChromaClient(DefaultConnectionName);
        }
        else
        {
            builder.AddChromaClient(DefaultConnectionName);
        }

        builder.Services.AddHttpClient(DefaultConnectionName).ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(requests));

        using var host = builder.Build();

        var client = useKeyed
            ? host.Services.GetRequiredKeyedService<ChromaClient>(DefaultConnectionName)
            : host.Services.GetRequiredService<ChromaClient>();
        await client.ListCollectionsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal("https://chroma.example/api/v2/tenants/my_tenant/databases/my_database/collections", request.RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Equal("secret", Assert.Single(request.Headers.GetValues("X-Chroma-Token")));
    }

    // The values the connection string does not have come from the configuration, and the settings given in code win over both.
    [Fact]
    public async Task TokenTenantAndDatabaseFromTheSettings()
    {
        List<HttpRequestMessage> requests = [];
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", "Endpoint=https://chroma.example;Tenant=from_string"),
            new KeyValuePair<string, string?>("Aspire:Chroma:Client:Token", "from_section"),
            new KeyValuePair<string, string?>("Aspire:Chroma:Client:Database", "from_section")
        ]);
        builder.AddChromaClient(DefaultConnectionName, settings => settings.Tenant = "from_code");
        builder.Services.AddHttpClient(DefaultConnectionName).ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(requests));

        using var host = builder.Build();

        await host.Services.GetRequiredService<ChromaClient>().ListCollectionsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal("https://chroma.example/api/v2/tenants/from_code/databases/from_section/collections", request.RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Equal("from_section", Assert.Single(request.Headers.GetValues("X-Chroma-Token")));
    }

    [Fact]
    public void ConnectionStringWithAnUnknownKeyThrows()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", "Endpoint=https://chroma.example;ApiKey=secret")
        ]);

        Assert.Throws<ArgumentException>(() => builder.AddChromaClient(DefaultConnectionName));
    }

    // The client is a singleton: its requests go through the current handler of IHttpClientFactory, which the factory renews.
    [Fact]
    public async Task RequestsUseTheCurrentHandlerOfTheFactory()
    {
        var handlers = 0;
        var builder = CreateBuilder();
        builder.AddChromaClient(DefaultConnectionName);
        builder.Services.AddHttpClient(DefaultConnectionName)
            .SetHandlerLifetime(TimeSpan.FromSeconds(1))
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                Interlocked.Increment(ref handlers);
                return new RecordingHandler([]);
            });

        using var host = builder.Build();

        var client = host.Services.GetRequiredService<ChromaClient>();
        await client.ListCollectionsAsync(cancellationToken: TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await client.ListCollectionsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, handlers);
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", DefaultConnectionString)
        ]);
        builder.Services.AddHttpClient();
        return builder;
    }

    // Answers every request with an empty list, and keeps the requests.
    private sealed class RecordingHandler(List<HttpRequestMessage> requests) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (requests)
            {
                requests.Add(request);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }
    }
}
