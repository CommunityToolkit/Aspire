using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace CommunityToolkit.Aspire.Minio.Client;

/// <summary>
/// Minio client configuration
/// </summary>
public sealed class MinioClientSettings
{
    private const string ConnectionStringEndpoint = "Endpoint";
    
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
    /// Minio client service lifetime
    /// </summary>
    public ServiceLifetime ServiceLifetime = ServiceLifetime.Singleton;
    
    /// <summary>
    /// Turn on tracing
    /// </summary>
    public bool SetTraceOn { get; set; } = true;
    
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
/// Minio credentials (access and secret keys)
/// </summary>
public class MinioCredentials
{
    /// <summary>
    /// Minio Access Key
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Minio Secret Key
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;
}