// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire;
using CommunityToolkit.Aspire.InfluxDB;
using InfluxDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering InfluxDB-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireInfluxDBExtensions
{
    private const string DefaultConfigSectionName = "Aspire:InfluxDB:Client";

    /// <summary>
    /// Registers <see cref="InfluxDBClient" /> as a singleton in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="InfluxDBClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:InfluxDB:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section</exception>
    public static void AddInfluxDBClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<InfluxDBClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(connectionName);
        AddInfluxDBClient(builder, DefaultConfigSectionName, configureSettings, connectionName, serviceKey: null);
    }

    /// <summary>
    /// Registers <see cref="InfluxDBClient" /> as a keyed singleton for the given <paramref name="name" /> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="InfluxDBClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:InfluxDB:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section</exception>
    public static void AddKeyedInfluxDBClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<InfluxDBClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(name);
        AddInfluxDBClient(builder, $"{DefaultConfigSectionName}:{name}", configureSettings, connectionName: name, serviceKey: name);
    }

    private static void AddInfluxDBClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<InfluxDBClientSettings>? configureSettings,
        string connectionName,
        string? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var settings = new InfluxDBClientSettings();
        builder.Configuration.GetSection(configurationSectionName).Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ParseConnectionString(connectionString);
        }

        configureSettings?.Invoke(settings);

        if (serviceKey is null)
        {
            builder.Services.AddSingleton(ConfigureInfluxDBClient);
        }
        else
        {
            builder.Services.AddKeyedSingleton(serviceKey, (sp, key) => ConfigureInfluxDBClient(sp));
        }

        if (!settings.DisableHealthChecks)
        {
            var healthCheckName = serviceKey is null ? "InfluxDB" : $"InfluxDB_{connectionName}";

            builder.TryAddHealthCheck(new HealthCheckRegistration(
                healthCheckName,
                sp => new InfluxDBHealthCheck(serviceKey is null ?
                    sp.GetRequiredService<InfluxDBClient>() :
                    sp.GetRequiredKeyedService<InfluxDBClient>(serviceKey)),
                failureStatus: null,
                tags: null,
                timeout: settings.HealthCheckTimeout > 0 ? TimeSpan.FromMilliseconds(settings.HealthCheckTimeout.Value) : null
                ));
        }

        InfluxDBClient ConfigureInfluxDBClient(IServiceProvider serviceProvider)
        {
            if (settings.Url is not null && settings.Token is not null)
            {
                var options = new InfluxDBClientOptions(settings.Url.ToString())
                {
                    Token = settings.Token,
                    Org = settings.Organization,
                    Bucket = settings.Bucket
                };
                return new InfluxDBClient(options);
            }
            else
            {
                throw new InvalidOperationException(
                        $"An InfluxDBClient could not be configured. Ensure valid connection information was provided in 'ConnectionStrings:{connectionName}' or " +
                        $"both {nameof(settings.Url)} and {nameof(settings.Token)} must be provided " +
                        $"in the '{configurationSectionName}' configuration section.");
            }
        }
    }
}
