using Microsoft.Extensions.Hosting;
using Minio;
using Microsoft.Extensions.Configuration;

namespace CommunityToolkit.Aspire.Minio.Client;

/// <summary>
/// Provides extension methods for registering MiniO-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class MinioClientBuilderExtensionMethods
{
    private const string DefaultConfigSectionName = "Aspire:Minio:Client";

    /// <summary>
    /// Adds Minio Client to ASPNet host
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configurationSectionName">Name of the configuration settings section</param>
    /// <param name="connectionName">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It is invoked after the settings are read from the configuration.</param>
    public static void AddMinioClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        string? configurationSectionName = DefaultConfigSectionName,
        Action<MinioClientSettings>? configureSettings = null)
    {
        var settings = GetMinioClientSettings(builder, configurationSectionName, configureSettings);

        builder.AddMinioInternal(connectionName, settings);
    }
    
    private static void AddMinioInternal(this IHostApplicationBuilder builder, string connectionName, MinioClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(settings);
        
        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ParseConnectionString(connectionString);
        }

        if (settings.Credentials is null)
        {
            var credentials = new MinioCredentials();
            credentials.SecretKey = builder.Configuration.GetValue<string>("Parameters:user") ?? "admin";
            credentials.AccessKey = builder.Configuration.GetValue<string>("Parameters:password") ?? "admin";
            
            settings.Credentials = credentials;
        }

        // Add the Minio client to the service collection.
        builder.Services.AddMinio(
            configureClient =>
            {
                var client = configureClient
                    .WithEndpoint(settings.Endpoint)
                    .WithSSL(settings.UseSsl);
                
                if (settings.Credentials is not null)
                    client.WithCredentials(settings.Credentials.AccessKey, settings.Credentials.SecretKey);
                
                if (settings.UserAgentHeaderInfo is not null)
                    client.SetAppInfo(settings.UserAgentHeaderInfo.AppName, settings.UserAgentHeaderInfo.AppVersion);
                        
                if (settings.SetTraceOn)
                    client.SetTraceOn();
                else
                    client.SetTraceOff();
            },
            settings.ServiceLifetime
            );
    }
    
    private static MinioClientSettings GetMinioClientSettings(IHostApplicationBuilder builder,
        string? configurationSectionName,
        Action<MinioClientSettings>? configureSettings)
    {
        var settings = new MinioClientSettings();

        builder.Configuration.Bind(configurationSectionName ?? DefaultConfigSectionName, settings);
        
        if (configureSettings is not null)
            configureSettings.Invoke(settings);

        return settings;
    }
}