using Aspire.Components.ConformanceTests;
using ChromaDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;

namespace CommunityToolkit.Aspire.Chroma.Tests;

// The activity test expects no span of the client before its own, so no other test of the client runs at the same time.
[CollectionDefinition(nameof(ConformanceTests), DisableParallelization = true)]
public class ConformanceTestsCollection;

[Collection(nameof(ConformanceTests))]
public class ConformanceTests : ConformanceTests<ChromaClient, ChromaClientSettings>
{
    // Nothing listens there: the operations fail at once and the health check reports Unhealthy.
    private const string Endpoint = "http://127.0.0.1:9";

    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;

    protected override string ActivitySourceName => ChromaTelemetry.ActivitySourceName;

    protected override string[] RequiredLogCategories => [];

    protected override bool SupportsKeyedRegistrations => true;

    protected override string? ConfigurationSectionName => "Aspire:Chroma:Client";

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>(CreateConfigKey("Aspire:Chroma:Client", key, "Endpoint"), Endpoint),
                new KeyValuePair<string, string?>($"ConnectionStrings:{key}", Endpoint)
            ]);
    }

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<ChromaClientSettings>? configure = null, string? key = null)
    {
        if (key is null)
        {
            builder.AddChromaClient("chroma", configureSettings: configure);
        }
        else
        {
            builder.AddKeyedChromaClient(key, configureSettings: configure);
        }
    }

    protected override void SetHealthCheck(ChromaClientSettings options, bool enabled)
    {
        options.DisableHealthChecks = !enabled;
    }

    protected override void SetTracing(ChromaClientSettings options, bool enabled)
    {
        options.DisableTracing = !enabled;
    }

    protected override void SetMetrics(ChromaClientSettings options, bool enabled)
    {
        options.DisableMetrics = !enabled;
    }

    protected override void TriggerActivity(ChromaClient service)
    {
        service.HeartbeatAsync().GetAwaiter().GetResult();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("key")]
    public void TracingReportsTheSpansOfTheClient(string? key) => ActivitySourceTest(key);

    [Theory]
    [InlineData(null)]
    [InlineData("key")]
    public void MetricsReportTheDurationOfTheOperations(string? key)
    {
        var builder = CreateHostBuilder(key: key);
        RegisterComponent(builder, options => SetMetrics(options, true), key);

        List<Metric> exportedMetrics = [];
        builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddInMemoryExporter(exportedMetrics));

        using var host = builder.Build();
        host.Start();

        var service = key is null
            ? host.Services.GetRequiredService<ChromaClient>()
            : host.Services.GetRequiredKeyedService<ChromaClient>(key);

        Assert.ThrowsAny<Exception>(() => TriggerActivity(service));
        host.Services.GetRequiredService<MeterProvider>().ForceFlush();

        Assert.Contains(exportedMetrics, metric => metric.Name == "db.client.operation.duration");
    }
}
