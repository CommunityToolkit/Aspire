using System;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.SeaweedFS.Client;

/// <summary>
/// Extension methods for connecting a SeaweedFS cluster to an Aspire application.
/// </summary>
public static class SeaweedFSClientBuilderExtensionMethods
{
    private const string DefaultConfigSectionName = "Aspire:SeaweedFS:Client";

    /// <summary>
    /// Registers a <see cref="IAmazonS3"/> client for interacting with the SeaweedFS S3 API.
    /// </summary>
    public static void AddSeaweedFSS3Client(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<SeaweedFSClientSettings>? configureSettings = null,
        string? configurationSectionName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        string sectionName = configurationSectionName ?? DefaultConfigSectionName;

        var settings = new SeaweedFSClientSettings();
        builder.Configuration.GetSection(sectionName).Bind(settings);
        builder.Configuration.GetSection($"{sectionName}:{connectionName}").Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ParseConnectionString(connectionString);
        }

        configureSettings?.Invoke(settings);

        builder.Services.AddKeyedSingleton<IAmazonS3>(connectionName, (sp, key) =>
        {
            if (settings.Endpoint is null)
            {
                throw new InvalidOperationException($"A valid absolute SeaweedFS endpoint URI must be provided. Ensure a valid connection string is registered for '{connectionName}'.");
            }

            // Dynamically override the endpoint scheme based on the explicit UseSsl property
            var uriBuilder = new UriBuilder(settings.Endpoint)
            {
                Scheme = settings.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp
            };

            // Remove default ports if they clash with the chosen scheme to ensure clean presigned URLs
            if ((settings.UseSsl && uriBuilder.Port == 80) || (!settings.UseSsl && uriBuilder.Port == 443))
            {
                uriBuilder.Port = -1;
            }

            var config = new AmazonS3Config
            {
                ServiceURL = uriBuilder.Uri.GetLeftPart(UriPartial.Authority),
                ForcePathStyle = settings.ForcePathStyle,
                UseHttp = !settings.UseSsl
            };

            settings.ConfigureS3Config?.Invoke(config);

            AWSCredentials credentials = (!string.IsNullOrWhiteSpace(settings.AccessKey) && !string.IsNullOrWhiteSpace(settings.SecretKey))
                ? new BasicAWSCredentials(settings.AccessKey, settings.SecretKey)
                : new AnonymousAWSCredentials();

            return new AmazonS3Client(credentials, config);
        });

        if (connectionName.Equals("seaweedfs", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.TryAddSingleton<IAmazonS3>(sp => sp.GetRequiredKeyedService<IAmazonS3>(connectionName));
        }

        if (!settings.DisableHealthChecks)
        {
            builder.Services.AddHealthChecks()
                .Add(new HealthCheckRegistration(
                    name: $"seaweedfs_s3_{connectionName}",
                    factory: sp => new SeaweedFSS3HealthCheck(sp.GetRequiredKeyedService<IAmazonS3>(connectionName)),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: ["seaweedfs", "storage", "s3"],
                    timeout: TimeSpan.FromSeconds(10)));
        }
    }

    /// <summary>
    /// Registers a <see cref="SeaweedFSFilerClient"/> for interacting with the SeaweedFS Native Filer API.
    /// </summary>
    public static void AddSeaweedFSFilerClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<SeaweedFSClientSettings>? configureSettings = null,
        string? configurationSectionName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        string sectionName = configurationSectionName ?? DefaultConfigSectionName;

        var settings = new SeaweedFSClientSettings();
        builder.Configuration.GetSection(sectionName).Bind(settings);
        builder.Configuration.GetSection($"{sectionName}:{connectionName}").Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ParseConnectionString(connectionString);
        }

        configureSettings?.Invoke(settings);

        Uri baseAddress = settings.FilerEndpoint ?? settings.Endpoint ?? new Uri($"http://{connectionName}");

        var uriBuilder = new UriBuilder(baseAddress)
        {
            Scheme = settings.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp
        };

        if ((settings.UseSsl && uriBuilder.Port == 80) || (!settings.UseSsl && uriBuilder.Port == 443))
        {
            uriBuilder.Port = -1;
        }

        builder.Services.AddHttpClient<SeaweedFSFilerClient>(client =>
        {
            client.BaseAddress = uriBuilder.Uri;
        });

        if (!settings.DisableHealthChecks)
        {
            builder.Services.AddHealthChecks()
                .Add(new HealthCheckRegistration(
                    name: $"seaweedfs_filer_{connectionName}",
                    factory: sp => new SeaweedFSFilerHealthCheck(sp.GetRequiredService<SeaweedFSFilerClient>()),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: ["seaweedfs", "storage", "filer"],
                    timeout: TimeSpan.FromSeconds(10)));
        }
    }
}