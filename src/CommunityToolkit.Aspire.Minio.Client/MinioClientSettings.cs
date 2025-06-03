using Microsoft.Extensions.DependencyInjection;
using Minio;
using System.Data.Common;

namespace CommunityToolkit.Aspire.Minio.Client;

/// <summary>
/// MinIO client configuration
/// </summary>
public sealed class MinioClientSettings
{
    private const string ConnectionStringEndpoint = "Endpoint";
    private const string AccessKey = "AccessKey";
    private const string SecretKey = "SecretKey";
    
    /// <summary>
    /// Endpoint URL
    /// </summary>
    public Uri? Endpoint { get; set; }
    
    /// <inheritdoc cref="MinioCredentials" />
    public MinioCredentials? Credentials { get; set; }
    
    /// <summary>
    /// Use ssl connection
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <inheritdoc cref="HeaderAppInformation" />
    public HeaderAppInformation? UserAgentHeaderInfo { get; set; }

    /// <summary>
    /// MinIO client service lifetime
    /// </summary>
    public ServiceLifetime ServiceLifetime = ServiceLifetime.Singleton;
    
    /// <summary>
    /// Turn on tracing.
    /// Isn't aspire tracing compatible yet. <see cref="MinioClient.SetTraceOn"/>
    /// </summary>
    public bool SetTraceOn { get; set; } = false;
    
    internal void ParseConnectionString(string? connectionString)
    {
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            Endpoint = uri;
        }
        else
        {
            var connectionBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (connectionBuilder.TryGetValue(ConnectionStringEndpoint, out var endpoint) 
                &&
                Uri.TryCreate(endpoint.ToString(), UriKind.Absolute, out var serviceUri))
            {
                Endpoint = serviceUri;
            }
            
            if (connectionBuilder.TryGetValue(AccessKey, out var accessKey)
                &&
                connectionBuilder.TryGetValue(SecretKey, out var secretKey)
                && 
                !string.IsNullOrEmpty(accessKey.ToString()) && !string.IsNullOrEmpty(secretKey.ToString()))
            {
                Credentials = new MinioCredentials
                {
                    AccessKey = accessKey.ToString()!, SecretKey = secretKey.ToString()!
                };
            }
        }
    }
}

/// <summary>
/// Sets app version and name. Used for constructing User-Agent header in all HTTP requests 
/// </summary>
public class HeaderAppInformation
{
    /// <summary>
    /// Set app name for MinioClient
    /// </summary>
    public string AppName { get; set; } = "CommunityToolkit.Aspire.Minio.Client";
    
    /// <summary>
    /// SetAppVersion for MinioClient
    /// </summary>
    public string AppVersion { get; set; } = "1.0";
}

/// <summary>
/// MinIO credentials (access and secret keys)
/// </summary>
public class MinioCredentials
{
    /// <summary>
    /// MinIO Access Key
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;
    
    /// <summary>
    /// MinIO Secret Key
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;
}