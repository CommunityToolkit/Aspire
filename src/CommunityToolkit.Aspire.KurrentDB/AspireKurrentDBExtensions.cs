// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire;
using CommunityToolkit.Aspire.KurrentDB;
using EventStore.Client;
using EventStore.Client.Extensions.OpenTelemetry;
using HealthChecks.EventStore.gRPC;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering KurrentDB-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireKurrentDBExtensions
{
    private const string DefaultConfigSectionName = "Aspire:KurrentDB:Client";

    /// <summary>
    /// Registers <see cref="EventStoreClient" /> as a singleton in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="KurrentDBSettings"/>. It's invoked after the settings are read from the configuration.</param>
    public static void AddKurrentDBClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<KurrentDBSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(connectionName);
        AddKurrentDBClient(builder, DefaultConfigSectionName, configureSettings, connectionName, serviceKey: null);
    }

    /// <summary>
    /// Registers <see cref="EventStoreClient" /> as a keyed singleton for the given <paramref name="name" /> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="KurrentDBSettings"/>. It's invoked after the settings are read from the configuration.</param>
    public static void AddKeyedKurrentDBClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<KurrentDBSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(name);
        AddKurrentDBClient(builder, $"{DefaultConfigSectionName}:{name}", configureSettings, connectionName: name, serviceKey: name);
    }

    private static void AddKurrentDBClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<KurrentDBSettings>? configureSettings,
        string connectionName,
        string? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var settings = new KurrentDBSettings();
        builder.Configuration.GetSection(configurationSectionName).Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        configureSettings?.Invoke(settings);

        if (serviceKey is null)
        {
            builder.Services.AddSingleton(ConfigureKurrentDBClient);
        }
        else
        {
            builder.Services.AddKeyedSingleton(serviceKey, (sp, key) => ConfigureKurrentDBClient(sp));
        }

        if (!settings.DisableTracing)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(traceBuilder => traceBuilder.AddEventStoreClientInstrumentation());
        }

        if (!settings.DisableHealthChecks)
        {
            var healthCheckName = serviceKey is null ? "KurrentDB.Client" : $"KurrentDB.Client_{connectionName}";

            builder.TryAddHealthCheck(new HealthCheckRegistration(
                healthCheckName,
                sp => new EventStoreHealthCheck(settings.ConnectionString!),
                failureStatus: default,
                tags: default,
                timeout: settings.HealthCheckTimeout));
        }

        EventStoreClient ConfigureKurrentDBClient(IServiceProvider serviceProvider)
        {
            if (settings.ConnectionString is not null)
            {
                var eventStoreClientSettings = EventStoreClientSettings.Create(settings.ConnectionString!);
                return new EventStoreClient(eventStoreClientSettings);
            }
            else
            {
                throw new InvalidOperationException(
                        $"A KurrentDB client could not be configured. Ensure valid connection information was provided in 'ConnectionStrings:{connectionName}' or either " +
                        $"{nameof(settings.ConnectionString)} must be provided " +
                        $"in the '{configurationSectionName}' configuration section.");
            }
        }
    }
}
