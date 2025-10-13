using CommunityToolkit.Aspire.Neon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for adding Neon database client to an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class NeonExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Neon";

    /// <summary>
    /// Registers an <see cref="NpgsqlDataSource"/> in the DI container for connecting to a Neon database.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to read config from and add services to.</param>
    /// <param name="connectionName">The name of the connection string in the configuration.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing the <see cref="NeonSettings"/>.</param>
    /// <param name="configureDataSourceBuilder">An optional delegate that can be used for customizing the <see cref="NpgsqlDataSourceBuilder"/>.</param>
    /// <remarks>
    /// <example>
    /// Add a Neon data source to the service collection.
    /// <code lang="csharp">
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// builder.AddNeonDataSource("neondb");
    ///
    /// var app = builder.Build();
    /// </code>
    /// </example>
    /// </remarks>
    public static void AddNeonDataSource(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<NeonSettings>? configureSettings = null,
        Action<NpgsqlDataSourceBuilder>? configureDataSourceBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        var configSectionPath = $"{DefaultConfigSectionName}:{connectionName}";
        var configSection = builder.Configuration.GetSection(configSectionPath);

        var settings = new NeonSettings();
        configSection.Bind(settings);
        configureSettings?.Invoke(settings);

        if (string.IsNullOrEmpty(settings.ConnectionString))
        {
            settings.ConnectionString = builder.Configuration.GetConnectionString(connectionName);
        }

        if (string.IsNullOrEmpty(settings.ConnectionString))
        {
            throw new InvalidOperationException($"Connection string '{connectionName}' not found.");
        }

        builder.AddNpgsqlDataSource(
            connectionName,
            configureSettings: npgsqlSettings =>
            {
                npgsqlSettings.ConnectionString = settings.ConnectionString;
                npgsqlSettings.DisableHealthChecks = settings.DisableHealthChecks;
                npgsqlSettings.DisableTracing = settings.DisableTracing;
                npgsqlSettings.DisableMetrics = settings.DisableMetrics;
            },
            configureDataSourceBuilder: configureDataSourceBuilder);
    }
}
