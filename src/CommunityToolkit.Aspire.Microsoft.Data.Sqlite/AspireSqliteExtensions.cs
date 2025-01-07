using Aspire;
using HealthChecks.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering Sqlite-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireSqliteExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Sqlite:Client";

    /// <summary>
    /// Registers <see cref="SqliteConnection" /> as a singleton in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="SqliteClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:Sqlite:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section.</exception>
    public static void AddSqliteClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<SqliteClientSettings>? configureSettings = null) =>
            AddSqliteClient(builder, DefaultConfigSectionName, configureSettings, name, serviceKey: null);

    /// <summary>
    /// Registers <see cref="SqliteConnection" /> as a keyed singleton for the given <paramref name="name" /> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="SqliteClientSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:Sqlite:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section.</exception>
    public static void AddKeyedSqliteClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<SqliteClientSettings>? configureSettings = null) =>
            AddSqliteClient(builder, $"{DefaultConfigSectionName}:{name}", configureSettings, connectionName: name, serviceKey: name);

    private static void AddSqliteClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<SqliteClientSettings>? configureSettings,
        string connectionName,
        object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        SqliteClientSettings settings = new();
        var configSection = builder.Configuration.GetSection(configurationSectionName);
        configSection.Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        configureSettings?.Invoke(settings);

        builder.RegisterSqliteServices(settings, connectionName, serviceKey);

        if (!settings.DisableHealthChecks)
        {
            builder.TryAddHealthCheck(new HealthCheckRegistration(
                serviceKey is null ? "Sqlite" : $"Sqlite_{connectionName}",
                sp =>
                {
                    var connection = serviceKey is null
                        ? sp.GetRequiredService<SqliteConnection>()
                        : sp.GetRequiredKeyedService<SqliteConnection>(serviceKey);
                    return new SqliteHealthCheck(
                        new SqliteHealthCheckOptions { ConnectionString = connection.ConnectionString });
                },
                failureStatus: default,
                tags: default,
                timeout: default));
        }
    }

    private static void RegisterSqliteServices(
        this IHostApplicationBuilder builder,
        SqliteClientSettings settings,
        string connectionName,
        object? serviceKey)
    {
        ConnectionStringValidation.ValidateConnectionString(settings.ConnectionString, connectionName, DefaultConfigSectionName);

        if (serviceKey is null)
        {
            builder.Services.AddScoped(sp => new SqliteConnection(settings.ConnectionString));
        }
        else
        {
            builder.Services.AddKeyedScoped(serviceKey, (_, _) => new SqliteConnection(settings.ConnectionString));
        }
    }
}
