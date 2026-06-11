using Amazon.Runtime;
using Amazon.S3;
using CommunityToolkit.Aspire.SeaweedFS.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.Hosting;
#pragma warning restore IDE0130

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

        SeaweedFSClientSettings settings = new();
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
            UriBuilder uriBuilder = new(settings.Endpoint)
            {
                Scheme = settings.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp
            };

            // Remove default ports if they clash with the chosen scheme to ensure clean presigned URLs
            if ((settings.UseSsl && uriBuilder.Port == 80) || (!settings.UseSsl && uriBuilder.Port == 443))
            {
                uriBuilder.Port = -1;
            }

            AmazonS3Config config = new()
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

        SeaweedFSClientSettings settings = new();
        builder.Configuration.GetSection(sectionName).Bind(settings);
        builder.Configuration.GetSection($"{sectionName}:{connectionName}").Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ParseConnectionString(connectionString);
        }

        configureSettings?.Invoke(settings);

        // Fallback for FilerEndpoint or generic Endpoint
        Uri baseAddress = settings.FilerEndpoint ?? settings.Endpoint ?? new Uri($"http://{connectionName}");

        UriBuilder uriBuilder = new(baseAddress)
        {
            Scheme = settings.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp
        };

        if ((settings.UseSsl && uriBuilder.Port == 80) || (!settings.UseSsl && uriBuilder.Port == 443))
        {
            uriBuilder.Port = -1;
        }

        // 1. Register the Named HttpClient
        builder.Services.AddHttpClient(connectionName, client =>
        {
            client.BaseAddress = uriBuilder.Uri;
        });

        // 2. Register the Keyed Service
        builder.Services.AddKeyedTransient<SeaweedFSFilerClient>(connectionName, (sp, key) =>
        {
            IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            return new SeaweedFSFilerClient(httpClientFactory.CreateClient(connectionName));
        });

        // TryAddTransient ensures the FIRST registered Filer becomes the default resolvable instance.
        builder.Services.TryAddTransient<SeaweedFSFilerClient>(sp => sp.GetRequiredKeyedService<SeaweedFSFilerClient>(connectionName));

        if (!settings.DisableHealthChecks)
        {
            builder.Services.AddHealthChecks()
                .Add(new HealthCheckRegistration(
                    name: $"seaweedfs_filer_{connectionName}",
                    factory: sp => new SeaweedFSFilerHealthCheck(sp.GetRequiredKeyedService<SeaweedFSFilerClient>(connectionName)),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: ["seaweedfs", "storage", "filer"],
                    timeout: TimeSpan.FromSeconds(10)));
        }
    }
}