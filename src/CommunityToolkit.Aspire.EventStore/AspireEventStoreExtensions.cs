// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire;
using CommunityToolkit.Aspire.EventStore;
using EventStore.Client;
using EventStore.Client.Extensions.OpenTelemetry;
using HealthChecks.EventStore.gRPC;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering EventStore-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireEventStoreExtensions
{
    private const string DefaultConfigSectionName = "Aspire:EventStore:Client";

    /// <summary>
    /// Registers <see cref="EventStoreClient" /> as a singleton in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="EventStoreSettings"/>. It's invoked after the settings are read from the configuration.</param>
    public static void AddEventStoreClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<EventStoreSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(connectionName);
        AddEventStoreClient(builder, DefaultConfigSectionName, configureSettings, connectionName, serviceKey: null);
    }

    /// <summary>
    /// Registers <see cref="EventStoreClient" /> as a keyed singleton for the given <paramref name="name" /> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="EventStoreSettings"/>. It's invoked after the settings are read from the configuration.</param>
    public static void AddKeyedEventStoreClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<EventStoreSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(name);
        AddEventStoreClient(builder, $"{DefaultConfigSectionName}:{name}", configureSettings, connectionName: name, serviceKey: name);
    }

    private static void AddEventStoreClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<EventStoreSettings>? configureSettings,
        string connectionName,
        string? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var settings = new EventStoreSettings();
        builder.Configuration.GetSection(configurationSectionName).Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        configureSettings?.Invoke(settings);

        if (serviceKey is null)
        {
            builder.Services.AddSingleton(ConfigureEventStoreClient);
        }
        else
        {
            builder.Services.AddKeyedSingleton(serviceKey, (sp, key) => ConfigureEventStoreClient(sp));
        }

        if (!settings.DisableTracing)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(traceBuilder => traceBuilder.AddEventStoreClientInstrumentation());
        }

        if (!settings.DisableHealthChecks)
        {
            var healthCheckName = serviceKey is null ? "EventStore.Client" : $"EventStore.Client_{connectionName}";

            builder.TryAddHealthCheck(new HealthCheckRegistration(
                healthCheckName,
                sp => new EventStoreHealthCheck(settings.ConnectionString!),
                failureStatus: default,
                tags: default,
                timeout: default));
        }

        EventStoreClient ConfigureEventStoreClient(IServiceProvider serviceProvider)
        {
            if (settings.ConnectionString is not null)
            {
                var eventStoreClientSettings = EventStoreClientSettings.Create(settings.ConnectionString!);
                return new EventStoreClient(eventStoreClientSettings);
            }
            else
            {
                throw new InvalidOperationException(
                        $"An EventStore could not be configured. Ensure valid connection information was provided in 'ConnectionStrings:{connectionName}' or either " +
                        $"{nameof(settings.ConnectionString)} must be provided " +
                        $"in the '{configurationSectionName}' configuration section.");
            }
        }
    }
}
