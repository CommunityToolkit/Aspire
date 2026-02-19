using System.Net.Http;
using ChromaDB.Client;
using CommunityToolkit.Aspire.Chroma;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering ChromaDB-related services.
/// </summary>
public static class AspireChromaExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Chroma";

    /// <summary>
    /// Registers <see cref="ChromaClient"/> as a singleton in the services collection.
    /// </summary>
    public static void AddChromaClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<ChromaClientSettings>? configureSettings = null)
    {
        AddChromaClient(builder, serviceKey: null, connectionName, configureSettings);
    }

    /// <summary>
    /// Registers <see cref="ChromaClient"/> as a keyed singleton in the services collection.
    /// </summary>
    public static void AddKeyedChromaClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<ChromaClientSettings>? configureSettings = null)
    {
        AddChromaClient(builder, serviceKey: name, connectionName: name, configureSettings);
    }

    private static void AddChromaClient(
        IHostApplicationBuilder builder,
        object? serviceKey,
        string connectionName,
        Action<ChromaClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(connectionName);

        var settings = new ChromaClientSettings();
        builder.Configuration.GetSection(DefaultConfigSectionName).Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ParseConnectionString(connectionString);
        }

        configureSettings?.Invoke(settings);

        if (serviceKey is null)
        {
            builder.Services.AddSingleton<ChromaClient>(sp => CreateClient(sp, settings, connectionName));
        }
        else
        {
            builder.Services.AddKeyedSingleton<ChromaClient>(serviceKey, (sp, key) => CreateClient(sp, settings, connectionName));
        }

        if (!settings.DisableHealthChecks)
        {
            builder.Services.AddHealthChecks()
                .Add(new HealthCheckRegistration(
                    serviceKey is null ? connectionName : $"{connectionName}_check",
                    sp => new ChromaHealthCheck(serviceKey is null
                        ? sp.GetRequiredService<ChromaClient>()
                        : sp.GetRequiredKeyedService<ChromaClient>(serviceKey)),
                    failureStatus: default,
                    tags: default,
                    timeout: settings.HealthCheckTimeout.HasValue ? TimeSpan.FromMilliseconds(settings.HealthCheckTimeout.Value) : default));
        }
    }

    private static ChromaClient CreateClient(IServiceProvider sp, ChromaClientSettings settings, string connectionName)
    {
        if (settings.Endpoint == null)
        {
            throw new InvalidOperationException($"ChromaDB endpoint is not configured for connection name '{connectionName}'.");
        }

        var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(connectionName);
        return new ChromaClient(new ChromaConfigurationOptions(settings.Endpoint.ToString()), httpClient);
    }
}
