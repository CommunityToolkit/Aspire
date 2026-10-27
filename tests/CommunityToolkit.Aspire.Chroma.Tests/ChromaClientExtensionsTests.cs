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

    // The Endpoint of a connection string needs its scheme: the error says so, instead of a missing endpoint.
    [Fact]
    public void ConnectionStringWithAnEndpointThatIsNotHttpThrows()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", "Endpoint=localhost:8000")
        ]);

        var exception = Assert.Throws<ArgumentException>(() => builder.AddChromaClient(DefaultConnectionName));
        Assert.Contains("http", exception.Message);
        Assert.DoesNotContain("missing", exception.Message);
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

    [Fact]
    public async Task KeyedClientsUseTheirOwnConnectionsAndHealthChecks()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:first", "http://first.example:8000"),
            new KeyValuePair<string, string?>("ConnectionStrings:second", "Endpoint=http://second.example:8001;Token=second-token")
        ]);

        builder.AddKeyedChromaClient("first");
        builder.AddKeyedChromaClient("second");

        using var host = builder.Build();
        var first = host.Services.GetRequiredKeyedService<ChromaClient>("first");
        var second = host.Services.GetRequiredKeyedService<ChromaClient>("second");

        Assert.NotSame(first, second);
        Assert.Equal("first.example", first.Options.Uri.Host);
        Assert.Equal("second.example", second.Options.Uri.Host);
        Assert.Equal(8001, second.Options.Uri.Port);
        Assert.True(string.IsNullOrEmpty(first.Options.ChromaToken));
        Assert.Equal("second-token", second.Options.ChromaToken);

        var registrations = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Contains(registrations, r => r.Name == "Chroma_first");
        Assert.Contains(registrations, r => r.Name == "Chroma_second");
    }

    // The Endpoint of the settings takes the same URIs as the Endpoint of a connection string: absolute, http or https.
    [Theory]
    [InlineData("ftp://localhost:8000")]
    [InlineData("localhost:8000")]
    [InlineData("relative/path")]
    public void AnEndpointSettingThatIsNotHttpThrowsWhenTheClientIsResolved(string endpoint)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("Aspire:Chroma:Client:Endpoint", endpoint)
        ]);

        builder.AddChromaClient(DefaultConnectionName);

        using var host = builder.Build();
        var exception = Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<ChromaClient>());
        Assert.Contains("http or https", exception.Message);
    }

    // The Endpoint of a connection string replaces the Endpoint of the settings, and the settings given in code win over both.
    [Theory]
    [InlineData(false, "from-string.example")]
    [InlineData(true, "from-code.example")]
    public void EndpointOfTheConnectionStringWinsOverTheSettings(bool setInCode, string expectedHost)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("Aspire:Chroma:Client:Endpoint", "http://from-settings.example:8000"),
            new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", "Endpoint=http://from-string.example:8000")
        ]);

        builder.AddChromaClient(DefaultConnectionName, settings =>
        {
            if (setInCode)
            {
                settings.Endpoint = new Uri("http://from-code.example:8000");
            }
        });

        using var host = builder.Build();
        Assert.Equal(expectedHost, host.Services.GetRequiredService<ChromaClient>().Options.Uri.Host);
    }
}
