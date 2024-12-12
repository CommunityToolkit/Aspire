// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire;
using CommunityToolkit.Aspire.SurrealDb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SurrealDb.Net;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering SurrealDB-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireSurrealDbExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Surreal:Client";
    
    /// <summary>
    /// Registers <see cref="SurrealDbClient" /> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="SurrealDbClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:Surreal:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section</exception>
    public static void AddSurrealClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<SurrealDbClientSettings>? configureSettings = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(connectionName);
        AddSurrealClient(builder, DefaultConfigSectionName, configureSettings, connectionName, serviceKey: null);
    }

    /// <summary>
    /// Registers <see cref="SurrealDbClient" /> as a keyed service for the given <paramref name="name" /> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="SurrealDbClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:Surreal:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section</exception>
    public static void AddKeyedSurrealClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<SurrealDbClientSettings>? configureSettings = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(name);
        AddSurrealClient(builder, $"{DefaultConfigSectionName}:{name}", configureSettings, connectionName: name, serviceKey: name);
    }

    private static void AddSurrealClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<SurrealDbClientSettings>? configureSettings,
        string connectionName,
        string? serviceKey
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        var settings = new SurrealDbClientSettings();
        builder.Configuration.GetSection(configurationSectionName).Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.Options = SurrealDbOptions.Create().FromConnectionString(connectionString).Build();
        }

        configureSettings?.Invoke(settings);

        if (settings.Options is null)
        {
            throw new NullReferenceException("SurrealDB configured options cannot be null.");
        }

        if (serviceKey is null)
        {
            builder.Services.AddSurreal(settings.Options, settings.Lifetime);
        }
        else
        {
            builder.Services.AddKeyedSurreal(serviceKey, settings.Options, settings.Lifetime);
        }

        if (!settings.DisableHealthChecks)
        {
            string healthCheckName = serviceKey is null ? "surrealdb" : $"surrealdb_{connectionName}";

            builder.TryAddHealthCheck(new HealthCheckRegistration(
                healthCheckName,
                sp => new SurrealDbHealthCheck(serviceKey is null ?
                    sp.GetRequiredService<SurrealDbClient>() :
                    sp.GetRequiredKeyedService<SurrealDbClient>(serviceKey)),
                failureStatus: null,
                tags: null,
                timeout: settings.HealthCheckTimeout > 0 ? TimeSpan.FromMilliseconds(settings.HealthCheckTimeout.Value) : null
                )
            );
        }
    }
}