using Aspire;
using DuckDB.NET.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering DuckDB-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireDuckDBExtensions
{
    private const string DefaultConfigSectionName = "Aspire:DuckDB:Client";

    /// <summary>
    /// Registers <see cref="DuckDBConnection" /> as scoped in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="DuckDBConnectionSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:DuckDB:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section.</exception>
    public static void AddDuckDBConnection(
        this IHostApplicationBuilder builder,
        string name,
        Action<DuckDBConnectionSettings>? configureSettings = null) =>
            AddDuckDBClient(builder, DefaultConfigSectionName, configureSettings, name, serviceKey: null);

    /// <summary>
    /// Registers <see cref="DuckDBConnection" /> as keyed scoped for the given <paramref name="name" /> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="DuckDBConnectionSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:DuckDB:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section.</exception>
    public static void AddKeyedDuckDBConnection(
        this IHostApplicationBuilder builder,
        string name,
        Action<DuckDBConnectionSettings>? configureSettings = null) =>
            AddDuckDBClient(builder, $"{DefaultConfigSectionName}:{name}", configureSettings, connectionName: name, serviceKey: name);

    private static void AddDuckDBClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<DuckDBConnectionSettings>? configureSettings,
        string connectionName,
        object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        DuckDBConnectionSettings settings = new();
        var configSection = builder.Configuration.GetSection(configurationSectionName);
        configSection.Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        configureSettings?.Invoke(settings);

        builder.RegisterDuckDBServices(settings, connectionName, serviceKey);

        if (!settings.DisableHealthChecks)
        {
            builder.TryAddHealthCheck(new HealthCheckRegistration(
                serviceKey is null ? "DuckDB" : $"DuckDB_{connectionName}",
                sp => new DuckDBHealthCheck(settings.ConnectionString),
                failureStatus: default,
                tags: default,
                timeout: default));
        }
    }

    private static void RegisterDuckDBServices(
        this IHostApplicationBuilder builder,
        DuckDBConnectionSettings settings,
        string connectionName,
        object? serviceKey)
    {
        if (serviceKey is null)
        {
            builder.Services.AddScoped(sp => CreateConnection(sp, null));
        }
        else
        {
            builder.Services.AddKeyedScoped(serviceKey, CreateConnection);
        }

        DuckDBConnection CreateConnection(IServiceProvider sp, object? key)
        {
            ConnectionStringValidation.ValidateConnectionString(settings.ConnectionString, connectionName, DefaultConfigSectionName);
            return new DuckDBConnection(settings.ConnectionString!);
        }
    }

    private sealed class DuckDBHealthCheck(string? connectionString) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new DuckDBConnection(connectionString ?? string.Empty);
                await connection.OpenAsync(cancellationToken);
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(exception: ex);
            }
        }
    }
}
