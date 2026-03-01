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
    private const string NeonEnvFilePrefix = "NEON_ENV_FILE";
    private const string NeonOutputDirKey = "NEON_OUTPUT_DIR";
    private const string NeonConnectionUriKey = "NEON_CONNECTION_URI=";

    /// <summary>
    /// Reads Neon provisioner <c>.env</c> files from the shared output volume and
    /// registers their connection URIs as standard <c>ConnectionStrings:{name}</c>
    /// entries in the application configuration.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to configure.</param>
    /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// In publish mode (Docker Compose, ACA, etc.) the Neon provisioner container
    /// writes per-database <c>.env</c> files to a shared Docker volume. Consumer
    /// containers receive environment variables pointing to these files
    /// (<c>NEON_ENV_FILE__{name}</c>) and the volume mount path
    /// (<c>NEON_OUTPUT_DIR</c>).
    /// </para>
    /// <para>
    /// Call this method early in <c>Program.cs</c> — before
    /// <c>AddNpgsqlDataSource</c>, <c>AddDbContext</c>, or any other
    /// client that reads <c>ConnectionStrings</c> — to make the
    /// provisioned connection strings available through standard
    /// configuration.
    /// </para>
    /// <para>
    /// If you are using <see cref="AddNeonClient(IHostApplicationBuilder, string, Action{NeonClientSettings}?)"/> the env-file
    /// resolution happens automatically; you do <b>not</b> need to call
    /// this method separately.
    /// </para>
    /// <example>
    /// <code lang="csharp">
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.AddNeonConnectionStrings();
    /// builder.AddNpgsqlDataSource("appdb"); // connection string is now resolved
    /// </code>
    /// </example>
    /// </remarks>
    public static IHostApplicationBuilder AddNeonConnectionStrings(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var envFileSection = builder.Configuration.GetSection(NeonEnvFilePrefix);
        foreach (var child in envFileSection.GetChildren())
        {
            string connectionName = child.Key;
            string? filePath = child.Value;

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                continue;
            }

            if (builder.Configuration.GetConnectionString(connectionName) is not null)
            {
                continue;
            }

            string? connectionUri = ParseNeonConnectionUri(filePath);
            if (connectionUri is not null)
            {
                builder.Configuration[$"ConnectionStrings:{connectionName}"] = connectionUri;
            }
        }

        string? outputDir = builder.Configuration[NeonOutputDirKey];
        if (outputDir is not null && Directory.Exists(outputDir))
        {
            foreach (string envFile in Directory.GetFiles(outputDir, "*.env"))
            {
                string dbName = Path.GetFileNameWithoutExtension(envFile);

                if (builder.Configuration.GetConnectionString(dbName) is not null)
                {
                    continue;
                }

                string? connectionUri = ParseNeonConnectionUri(envFile);
                if (connectionUri is not null)
                {
                    builder.Configuration[$"ConnectionStrings:{dbName}"] = connectionUri;
                }
            }
        }

        return builder;
    }

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

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            settings.ConnectionString = TryReadProvisionerConnectionString(
                builder.Configuration, connectionName);
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

    /// <summary>
    /// Attempts to read a Neon connection string from the provisioner env file.
    /// Checks <c>NEON_ENV_FILE__{connectionName}</c> first, then falls back to
    /// <c>NEON_OUTPUT_DIR/{connectionName}.env</c>.
    /// </summary>
    private static string? TryReadProvisionerConnectionString(
        IConfiguration configuration, string connectionName)
    {
        string? envFilePath = configuration[$"{NeonEnvFilePrefix}:{connectionName}"];

        if (envFilePath is null)
        {
            string? outputDir = configuration[NeonOutputDirKey];
            if (outputDir is not null)
            {
                envFilePath = Path.Combine(outputDir, $"{connectionName}.env");
            }
        }

        if (envFilePath is null || !File.Exists(envFilePath))
        {
            return null;
        }

        return ParseNeonConnectionUri(envFilePath);
    }

    /// <summary>
    /// Parses a provisioner <c>.env</c> file and returns the
    /// <c>NEON_CONNECTION_URI</c> value, or <see langword="null"/>.
    /// </summary>
    private static string? ParseNeonConnectionUri(string envFilePath)
    {
        foreach (string line in File.ReadAllLines(envFilePath))
        {
            if (line.StartsWith(NeonConnectionUriKey, StringComparison.Ordinal))
            {
                return line[NeonConnectionUriKey.Length..].Trim();
            }
        }

        return null;
    }
}