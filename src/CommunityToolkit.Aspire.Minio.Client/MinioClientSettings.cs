using System;
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
    private static readonly string[] UseSslKeys = new[] { "UseSsl", "UseSSL", "Ssl" };
    
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
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        // If the connection string is an absolute URI, use it as endpoint.
        // NOTE: Do NOT infer UseSsl from the URI scheme.
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            Endpoint = uri;
            return;
        }

        var connectionBuilder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        if (connectionBuilder.TryGetValue(ConnectionStringEndpoint, out var endpoint) 
            &&
            Uri.TryCreate(endpoint.ToString(), UriKind.Absolute, out var serviceUri))
        {
            Endpoint = serviceUri;
            // Intentionally do not set UseSsl here based on the scheme.
        }
        
        // Check for UseSsl (and variants) in the connection string and parse it if present.
        foreach (var key in UseSslKeys)
        {
            if (connectionBuilder.TryGetValue(key, out var useSslObj))
            {
                if (TryParseBool(useSslObj, out var parsed))
                {
                    UseSsl = parsed;
                    break;
                }
            }
        }
        
        if (connectionBuilder.TryGetValue(AccessKey, out var accessKey)
            &&
            connectionBuilder.TryGetValue(SecretKey, out var secretKey)
            && 
            !string.IsNullOrEmpty(accessKey?.ToString()) && !string.IsNullOrEmpty(secretKey?.ToString()))
        {
            Credentials = new MinioCredentials
            {
                AccessKey = accessKey.ToString()!, SecretKey = secretKey.ToString()!
            };
        }
    }

    // Strict boolean parsing: only accepts bool or the strings "true"/"false" (case-insensitive).
    private static bool TryParseBool(object? value, out bool result)
    {
        result = false;
        if (value == null) return false;

        if (value is bool b)
        {
            result = b;
            return true;
        }

        var s = value.ToString();
        if (s == null) return false;

        s = s.Trim();

        if (bool.TryParse(s, out var parsedBool))
        {
            result = parsedBool;
            return true;
        }

        return false;
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