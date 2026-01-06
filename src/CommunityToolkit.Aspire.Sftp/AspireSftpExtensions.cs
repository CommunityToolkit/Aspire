// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire;
using CommunityToolkit.Aspire.Sftp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Renci.SshNet;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering SFTP-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireSftpExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Sftp:Client";

    /// <summary>
    /// Registers <see cref="SftpClient" /> as a singleton in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="SftpSettings"/>. It's invoked after the settings are read from the configuration.</param>
    public static void AddSftpClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<SftpSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);
        AddSftpClient(builder, DefaultConfigSectionName, configureSettings, connectionName, serviceKey: null);
    }

    /// <summary>
    /// Registers <see cref="SftpClient" /> as a keyed singleton for the given <paramref name="name" /> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="SftpSettings"/>. It's invoked after the settings are read from the configuration.</param>
    public static void AddKeyedSftpClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<SftpSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddSftpClient(builder, $"{DefaultConfigSectionName}:{name}", configureSettings, connectionName: name, serviceKey: name);
    }

    private static void AddSftpClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<SftpSettings>? configureSettings,
        string connectionName,
        string? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var settings = new SftpSettings();
        builder.Configuration.GetSection(configurationSectionName).Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        configureSettings?.Invoke(settings);

        if (serviceKey is null)
        {
            builder.Services.AddSingleton(ConfigureSftpClient);
        }
        else
        {
            builder.Services.AddKeyedSingleton(serviceKey, (sp, key) => ConfigureSftpClient(sp));
        }

        if (!settings.DisableTracing)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(traceBuilder => traceBuilder.AddSource("Renci.SshNet"));
        }

        if (!settings.DisableHealthChecks)
        {
            var healthCheckName = serviceKey is null ? "Sftp.Client" : $"Sftp.Client_{connectionName}";

            builder.TryAddHealthCheck(new HealthCheckRegistration(
                healthCheckName,
                sp => new SftpHealthCheck(settings),
                failureStatus: default,
                tags: default,
                timeout: settings.HealthCheckTimeout));
        }

        SftpClient ConfigureSftpClient(IServiceProvider serviceProvider)
        {
            if (settings.ConnectionString is not null)
            {
                var (host, port) = ParseConnectionString(settings.ConnectionString);

                if (string.IsNullOrEmpty(settings.Username))
                {
                    throw new InvalidOperationException(
                        $"An SFTP client could not be configured. The '{nameof(settings.Username)}' must be provided " +
                        $"in the '{configurationSectionName}' configuration section.");
                }

                ConnectionInfo connectionInfo;

                if (!string.IsNullOrEmpty(settings.PrivateKeyFile))
                {
                    var privateKeyFile = new PrivateKeyFile(settings.PrivateKeyFile);
                    connectionInfo = new ConnectionInfo(host, port, settings.Username, new PrivateKeyAuthenticationMethod(settings.Username, privateKeyFile));
                }
                else if (!string.IsNullOrEmpty(settings.Password))
                {
                    connectionInfo = new ConnectionInfo(host, port, settings.Username, new PasswordAuthenticationMethod(settings.Username, settings.Password));
                }
                else
                {
                    throw new InvalidOperationException(
                        $"An SFTP client could not be configured. Either '{nameof(settings.Password)}' or '{nameof(settings.PrivateKeyFile)}' must be provided " +
                        $"in the '{configurationSectionName}' configuration section.");
                }

                var client = new SftpClient(connectionInfo);
                return client;
            }
            else
            {
                throw new InvalidOperationException(
                    $"An SFTP client could not be configured. Ensure valid connection information was provided in 'ConnectionStrings:{connectionName}' or " +
                    $"{nameof(settings.ConnectionString)} must be provided " +
                    $"in the '{configurationSectionName}' configuration section.");
            }
        }
    }

    private static (string Host, int Port) ParseConnectionString(string connectionString)
    {
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 22;
            return (host, port);
        }

        throw new InvalidOperationException($"The connection string '{connectionString}' is not in the correct format. Expected format: 'sftp://host:port'");
    }
}
