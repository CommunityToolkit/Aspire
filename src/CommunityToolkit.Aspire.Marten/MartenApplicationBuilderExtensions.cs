using Aspire;
using CommunityToolkit.Aspire.Marten;
using HealthChecks.NpgSql;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// TODO
/// </summary>
public static class MartenApplicationBuilderExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Marten";

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="connectionName"></param>
    /// <param name="configureSettings"></param>
    /// <param name="configureStoreOptions"></param>
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
    /// TOOD
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="configureSettings"></param>
    /// <param name="configureStoreOptions"></param>
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
                })
                .WithMetrics(metrics =>
                {
                    metrics.AddMeter("Marten");
                });
        }

        if (!settings.DisableMetrics)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing.AddSource("Marten");
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

    private static void ValidateConnectionString(string? connectionString, string connectionName, string defaultConfigSectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"ConnectionString is missing. It should be provided in 'ConnectionStrings:{connectionName}' or under the 'ConnectionString' key in '{defaultConfigSectionName}' configuration section.");
        }
    }
}