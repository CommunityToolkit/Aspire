using Aspire;
using ChromaDB.Client;
using ChromaDB.Client.DependencyInjection;
using CommunityToolkit.Aspire.Chroma;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering ChromaDB-related services.
/// </summary>
public static class AspireChromaExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Chroma:Client";

    /// <summary>
    /// Registers <see cref="ChromaClient"/> as a singleton in the services collection.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="ChromaClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    public static void AddChromaClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<ChromaClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(connectionName);
        AddChromaClient(builder, DefaultConfigSectionName, configureSettings, connectionName, serviceKey: null);
    }

    /// <summary>
    /// Registers <see cref="ChromaClient"/> as a keyed singleton in the services collection.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="ChromaClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    public static void AddKeyedChromaClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<ChromaClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(name);
        AddChromaClient(builder, $"{DefaultConfigSectionName}:{name}", configureSettings, connectionName: name, serviceKey: name);
    }

    private static void AddChromaClient(
        IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<ChromaClientSettings>? configureSettings,
        string connectionName,
        object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSectionName, nameof(configurationSectionName));
        ArgumentNullException.ThrowIfNull(connectionName, nameof(connectionName));

        var settings = new ChromaClientSettings();
        builder.Configuration.GetSection(configurationSectionName).Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ParseConnectionString(connectionString);
        }

        configureSettings?.Invoke(settings);

        // The client sends each request with the current handler of the HttpClient of this name of IHttpClientFactory.
        builder.Services.AddHttpClient(connectionName);

        if (serviceKey is null)
        {
            builder.Services.AddSingleton<ChromaClient>(sp => CreateClient(sp, settings, connectionName));
        }
        else
        {
            builder.Services.AddKeyedSingleton<ChromaClient>(serviceKey, (sp, key) => CreateClient(sp, settings, connectionName));
        }

        if (!settings.DisableTracing)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddSource(ChromaTelemetry.ActivitySourceName));
        }

        if (!settings.DisableMetrics)
        {
            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics => metrics.AddMeter(ChromaTelemetry.MeterName));
        }

        if (!settings.DisableHealthChecks)
        {
            var healthCheckName = serviceKey is null ? "Chroma" : $"Chroma_{connectionName}";

            builder.TryAddHealthCheck(new HealthCheckRegistration(
                healthCheckName,
                sp => new ChromaHealthCheck(serviceKey is null
                    ? sp.GetRequiredService<ChromaClient>()
                    : sp.GetRequiredKeyedService<ChromaClient>(serviceKey)),
                failureStatus: null,
                tags: null,
                timeout: settings.HealthCheckTimeout > 0 ? TimeSpan.FromMilliseconds(settings.HealthCheckTimeout.Value) : null));
        }
    }

    private static ChromaClient CreateClient(IServiceProvider sp, ChromaClientSettings settings, string connectionName)
    {
        if (settings.Endpoint is null)
        {
            throw new InvalidOperationException($"ChromaDB endpoint is not configured for connection name '{connectionName}'.");
        }

        // An endpoint with only the address of the server, like http://localhost:8000, gets the path of the v2 API.
        var options = new ChromaConfigurationOptions(settings.Endpoint, tenant: settings.Tenant, database: settings.Database, chromaToken: settings.Token);

        return sp.CreateChromaClient(options, connectionName);
    }
}
