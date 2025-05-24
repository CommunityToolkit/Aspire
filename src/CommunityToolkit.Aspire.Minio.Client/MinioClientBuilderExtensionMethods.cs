using CommunityToolkit.Aspire.Minio.Client;
using Minio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering MiniO-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class MinioClientBuilderExtensionMethods
{
    private const string DefaultConfigSectionName = "Aspire:Minio:Client";

    /// <summary>
    /// Adds Minio Client to ASPNet host
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> used to add services.</param>
    /// <param name="configurationSectionName">Name of the configuration settings section</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It is invoked after the settings are read from the configuration.</param>
    public static void AddMinioClient(
        this IHostApplicationBuilder builder,
        string? connectionName = null,
        string? configurationSectionName = DefaultConfigSectionName,
        Action<MinioClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        
        var settings = GetMinioClientSettings(builder, connectionName, configurationSectionName, configureSettings);

        builder.AddMinioInternal(settings);
    }
    
    private static void AddMinioInternal(this IHostApplicationBuilder builder, MinioClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Add the Minio client to the service collection.
        void ConfigureClient(IMinioClient configureClient)
        {
            var client = configureClient.WithEndpoint(settings.Endpoint)
                .WithSSL(settings.UseSsl);

            if (settings.Credentials is not null) client.WithCredentials(settings.Credentials.AccessKey, settings.Credentials.SecretKey);

            if (settings.UserAgentHeaderInfo is not null) client.SetAppInfo(settings.UserAgentHeaderInfo.AppName, settings.UserAgentHeaderInfo.AppVersion);

            if (settings.SetTraceOn)
                client.SetTraceOn();
            else
                client.SetTraceOff();
        }

        var minioClientFactory = new MinioClientFactory(ConfigureClient);
        builder.Services.TryAddSingleton<IMinioClientFactory>(minioClientFactory);
        
        IMinioClient GetClient()
        {
            if (settings.Endpoint is null)
                throw new InvalidOperationException("The MiniO endpoint must be provided either in configuration section, or as a part of connection string or settings delegate");
            
            return minioClientFactory.CreateClient();
        }
        
        switch (settings.ServiceLifetime)
        {
            case ServiceLifetime.Singleton:
                builder.Services.TryAddSingleton(_ => GetClient());
                break;
            case ServiceLifetime.Scoped:
                builder.Services.TryAddScoped(_ => GetClient());
                break;
            case ServiceLifetime.Transient:
                builder.Services.TryAddTransient(_ => GetClient());
                break;
        }
    }
    
    private static MinioClientSettings GetMinioClientSettings(IHostApplicationBuilder builder,
        string? connectionName,
        string? configurationSectionName,
        Action<MinioClientSettings>? configureSettings)
    {
        var settings = new MinioClientSettings();

        builder.Configuration.Bind(configurationSectionName ?? DefaultConfigSectionName, settings);
        
        if (!string.IsNullOrEmpty(connectionName) &&
            builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ParseConnectionString(connectionString);
        }
        
        configureSettings?.Invoke(settings);
        
        return settings;
    }
}