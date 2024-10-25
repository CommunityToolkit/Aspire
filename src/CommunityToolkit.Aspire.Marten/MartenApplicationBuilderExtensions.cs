using Aspire;
using CommunityToolkit.Aspire.Marten;
using HealthChecks.NpgSql;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for adding and configuring Marten in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class MartenApplicationBuilderExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Marten";

    /// <summary>
    /// Registers <see cref="DocumentStore" /> as a singleton in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <remarks>Reads the configuration from "Aspire:Marten" section.</remarks>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="MartenSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <param name="configureStoreOptions">An optional action to configure <see cref="StoreOptions"/>.</param>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section</exception>
    [Experimental("CTASPIREMARTEN001", UrlFormat = "https://aka.ms/communitytoolkit/aspire/diagnostics#{0}")]
    public static void AddMartenClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<MartenSettings>? configureSettings = null,
        Action<StoreOptions>? configureStoreOptions = null
        )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        builder.AddMartenClient(DefaultConfigSectionName, configureSettings, configureStoreOptions, connectionName, serviceKey: null);
    }

    /// <summary>
    /// Registers <see cref="DocumentStore" /> as a keyed singleton for the given <paramref name="name" /> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <remarks>Reads the configuration from "Aspire:Marten" section.</remarks>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="MartenSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <param name="configureStoreOptions">An optional action to configure <see cref="StoreOptions"/>.</param>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section</exception>

    [Experimental("CTASPIREMARTEN001", UrlFormat = "https://aka.ms/communitytoolkit/aspire/diagnostics#{0}")]
    public static void AddKeyedMartenClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<MartenSettings>? configureSettings = null,
        Action<StoreOptions>? configureStoreOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.AddMartenClient(
            $"{DefaultConfigSectionName}:{name}",
            configureSettings,
            configureStoreOptions,
            connectionName: name,
            serviceKey: name);
    }

    /// <summary>
    /// Adds a Marten client to the application builder with the specified configuration.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to add the Marten client to.</param>
    /// <param name="configurationSectionName">The name of the configuration section to use.</param>
    /// <param name="configureSettings">An optional action to configure <see cref="MartenSettings"/>.</param>
    /// <param name="configureStoreOptions">An optional action to configure <see cref="StoreOptions"/>.</param>
    /// <param name="connectionName">The name of the connection string to use.</param>
    /// <param name="serviceKey">An optional service key for the Marten client.</param>
    private static void AddMartenClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<MartenSettings>? configureSettings,
        Action<StoreOptions>? configureStoreOptions,
        string connectionName,
        object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var configSection = builder.Configuration.GetSection(configurationSectionName);

        MartenSettings settings = new();
        configSection.Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        configureSettings?.Invoke(settings);

        builder.RegisterMartenServices(settings, configurationSectionName, connectionName, serviceKey, configureStoreOptions);

        if (!settings.DisableTracing)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing.AddSource("Marten");
                });
        }

        if (!settings.DisableMetrics)
        {
            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddMeter("Marten");
                });
        }

        if (!settings.DisableHealthChecks)
        {
            builder.TryAddHealthCheck(new HealthCheckRegistration(
                serviceKey is null ? "Aspire.Marten" : $"Aspire.Marten_{connectionName}",
                sp => new NpgSqlHealthCheck(
                    new NpgSqlHealthCheckOptions(serviceKey is null
                        ? sp.GetRequiredKeyedService<NpgsqlDataSource>("Aspire.Marten")
                        : sp.GetRequiredKeyedService<NpgsqlDataSource>($"Aspire.Marten_{connectionName}"))),
                failureStatus: default,
                tags: default,
                timeout: settings.HealthCheckTimeout > 0 ? TimeSpan.FromMilliseconds(settings.HealthCheckTimeout.Value) : null));
        }
    }

    /// <summary>
    /// Registers Marten services with the specified settings and configuration.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to register the services with.</param>
    /// <param name="settings">The <see cref="MartenSettings"/> to use.</param>
    /// <param name="configurationSectionName">The name of the configuration section to use.</param>
    /// <param name="connectionName">The name of the connection string to use.</param>
    /// <param name="serviceKey">An optional service key for the Marten client.</param>
    /// <param name="configureStoreOptions">An optional action to configure <see cref="StoreOptions"/>.</param>
    private static void RegisterMartenServices(this IHostApplicationBuilder builder, MartenSettings settings, string configurationSectionName, string connectionName, object? serviceKey, Action<StoreOptions>? configureStoreOptions)
    {
        builder.Services.AddKeyedSingleton<NpgsqlDataSource>(serviceKey is null ? "Aspire.Marten" : $"Aspire.Marten_{connectionName}", (serviceProvider, _) =>
        {
            ValidateConnectionString(settings.ConnectionString, connectionName, DefaultConfigSectionName);

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(settings.ConnectionString);
            dataSourceBuilder.UseLoggerFactory(serviceProvider.GetService<ILoggerFactory>());

            return dataSourceBuilder.Build();
        });

        if (serviceKey is null)
        {
            builder.Services.AddSingleton<DocumentStore>(sp =>
            {
                var store = DocumentStore.For((storeOption) =>
                {
                    ValidateConnectionString(settings.ConnectionString, connectionName, DefaultConfigSectionName);

                    configureStoreOptions?.Invoke(storeOption);
                    storeOption.Connection(settings.ConnectionString!);
                });
                return store;
            });
            builder.Services.AddSingleton<IDocumentSession>(sp =>
            {
                return sp.GetRequiredService<DocumentStore>().LightweightSession();
            });
            builder.Services.AddScoped<IQuerySession>(sp =>
            {
                return sp.GetRequiredService<DocumentStore>().QuerySession();
            });
        }
        else
        {
            builder.Services.AddKeyedSingleton<DocumentStore>(serviceKey, (sp, _) =>
            {
                var store = DocumentStore.For((storeOption) =>
                {
                    ValidateConnectionString(settings.ConnectionString, connectionName, DefaultConfigSectionName);

                    configureStoreOptions?.Invoke(storeOption);
                    storeOption.Connection(settings.ConnectionString!);
                });
                return store;
            });
            builder.Services.AddKeyedSingleton<IDocumentSession>(serviceKey, (sp, _) =>
            {
                return sp.GetRequiredService<DocumentStore>().LightweightSession();
            });
            builder.Services.AddKeyedScoped<IQuerySession>(serviceKey, (sp, _) =>
            {
                return sp.GetRequiredService<DocumentStore>().QuerySession();
            });
        }
    }

    /// <summary>
    /// Validates the specified connection string.
    /// </summary>
    /// <param name="connectionString">The connection string to validate.</param>
    /// <param name="connectionName">The name of the connection string.</param>
    /// <param name="defaultConfigSectionName">The default configuration section name.</param>
    /// <exception cref="InvalidOperationException">Thrown if the connection string is null or whitespace.</exception>
    private static void ValidateConnectionString(string? connectionString, string connectionName, string defaultConfigSectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"ConnectionString is missing. It should be provided in 'ConnectionStrings:{connectionName}' or under the 'ConnectionString' key in '{defaultConfigSectionName}' configuration section.");
        }
    }
}
