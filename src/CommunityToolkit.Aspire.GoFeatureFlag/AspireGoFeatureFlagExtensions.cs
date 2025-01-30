// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire;
using CommunityToolkit.Aspire.GoFeatureFlag;
using HealthChecks.Uris;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenFeature.Contrib.Providers.GOFeatureFlag;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering GO Feature Flag-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireGoFeatureFlagExtensions
{
    private const string DefaultConfigSectionName = "Aspire:GoFeatureFlag:Client";

    /// <summary>
    /// Registers <see cref="GoFeatureFlagClientSettings" /> as a singleton in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="GoFeatureFlagProviderOptions"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:GoFeatureFlag:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section</exception>
    public static void AddGoFeatureFlagClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<GoFeatureFlagClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(connectionName);
        AddGoFeatureFlagClient(builder, DefaultConfigSectionName, configureSettings, connectionName, serviceKey: null);
    }

    /// <summary>
    /// Registers <see cref="GoFeatureFlagClientSettings" /> as a keyed singleton for the given <paramref name="name" /> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="GoFeatureFlagProviderOptions"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:GoFeatureFlag:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section</exception>
    public static void AddKeyedGoFeatureFlagClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<GoFeatureFlagClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrEmpty(name);
        AddGoFeatureFlagClient(builder, $"{DefaultConfigSectionName}:{name}", configureSettings, connectionName: name, serviceKey: name);
    }

    private static void AddGoFeatureFlagClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<GoFeatureFlagClientSettings>? configureSettings,
        string connectionName,
        string? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var settings = new GoFeatureFlagClientSettings();

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ParseConnectionString(connectionString);
        }

        configureSettings?.Invoke(settings);
        
        if (settings.Endpoint is not null && string.IsNullOrEmpty(settings.ProviderOptions.Endpoint))
        {
            settings.ProviderOptions.Endpoint = settings.Endpoint.ToString();
        }

        if (serviceKey is null)
        {
            builder.Services.AddSingleton((sp) => ConfigureGoFeatureFlagClient(settings.ProviderOptions));
        }
        else
        {
            builder.Services.AddKeyedSingleton(serviceKey, (sp, key) => ConfigureGoFeatureFlagClient(settings.ProviderOptions));
        }

        if (settings is { DisableHealthChecks: false, Endpoint: not null })
        {
            var healthCheckName = serviceKey is null ? "Goff" : $"Goff_{connectionName}";
            
            var healthEndpoint = new Uri(settings.Endpoint, "/health");
            var uriHealthCheck = new UriHealthCheck(
                new UriHealthCheckOptions().AddUri(healthEndpoint),
                () => new HttpClient()
            );
            
            builder.TryAddHealthCheck(new HealthCheckRegistration(
                healthCheckName,
                sp => uriHealthCheck,
                failureStatus: null,
                tags: null,
                timeout: settings.HealthCheckTimeout > 0 ? TimeSpan.FromMilliseconds(settings.HealthCheckTimeout.Value) : null
                ));
        }

        GoFeatureFlagProvider ConfigureGoFeatureFlagClient(GoFeatureFlagProviderOptions options)
        {
            if (settings.Endpoint is not null)
            {
                return new GoFeatureFlagProvider(options);
            }
            
            throw new InvalidOperationException(
                    $"A GoFeatureFlagProvider could not be configured. Ensure valid connection information was provided in 'ConnectionStrings:{connectionName}' or either " +
                    $"{nameof(settings.Endpoint)} must be provided " +
                    $"in the '{configurationSectionName}' configuration section.");
        }
    }
}
