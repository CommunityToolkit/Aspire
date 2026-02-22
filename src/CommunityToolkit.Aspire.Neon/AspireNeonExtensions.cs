using Aspire;
using CommunityToolkit.Aspire.Neon;
using HealthChecks.NpgSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering Neon-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireNeonExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Neon:Client";

    /// <summary>
    /// Registers <see cref="NpgsqlDataSource"/> for Neon in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to read config from and add services to.</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="NeonClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:Neon:Client" section.</remarks>
    public static void AddNeonClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<NeonClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        AddNeonClient(builder, DefaultConfigSectionName, configureSettings, connectionName, serviceKey: null);
    }

    /// <summary>
    /// Registers <see cref="NpgsqlDataSource"/> for Neon as a keyed service for the given <paramref name="connectionName"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to read config from and add services to.</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="NeonClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:Neon:Client" section.</remarks>
    public static void AddKeyedNeonClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<NeonClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        AddNeonClient(builder, $"{DefaultConfigSectionName}:{connectionName}", configureSettings, connectionName, serviceKey: connectionName);
    }

    private static void AddNeonClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<NeonClientSettings>? configureSettings,
        string connectionName,
        object? serviceKey)
    {
        NeonClientSettings settings = new();
        builder.Configuration.GetSection(configurationSectionName).Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        configureSettings?.Invoke(settings);

        ConnectionStringValidation.ValidateConnectionString(settings.ConnectionString, connectionName, DefaultConfigSectionName, configurationSectionName);

        if (serviceKey is null)
        {
            builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(settings.ConnectionString!));
        }
        else
        {
            builder.Services.AddKeyedSingleton(serviceKey, () => NpgsqlDataSource.Create(settings.ConnectionString!));
        }

        if (!settings.DisableHealthChecks)
        {
            string healthCheckName = serviceKey is null ? "neon" : $"neon_{connectionName}";

            builder.TryAddHealthCheck(new HealthCheckRegistration(
                healthCheckName,
                _ => new NpgSqlHealthCheck(new NpgSqlHealthCheckOptions(settings.ConnectionString!)),
                failureStatus: default,
                tags: default,
                timeout: settings.HealthCheckTimeout > 0 ? TimeSpan.FromMilliseconds(settings.HealthCheckTimeout.Value) : null));
        }
    }
}