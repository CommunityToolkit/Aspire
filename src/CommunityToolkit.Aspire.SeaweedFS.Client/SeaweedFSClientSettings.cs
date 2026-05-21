using System;
using System.Data.Common;
using Amazon.S3;

namespace CommunityToolkit.Aspire.SeaweedFS.Client;

/// <summary>
/// Provides the settings for configuring the SeaweedFS S3 client.
/// </summary>
public sealed class SeaweedFSClientSettings
{
    private const string ConnectionStringEndpoint = "Endpoint";
    private const string AccessKeyName = "AccessKey";
    private const string SecretKeyName = "SecretKey";
    private const string ConnectionStringFilerEndpoint = "FilerEndpoint";
    private const string ConnectionStringFilerUrl = "FilerUrl";
    private const string UseSslKey = "UseSsl";

    /// <summary>
    /// Gets or sets the endpoint URL of the SeaweedFS S3 API.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the endpoint URL for the SeaweedFS Filer API.
    /// </summary>
    public Uri? FilerEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the S3 Access Key.
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// Gets or sets the S3 Secret Key.
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to force path style URLs for S3 objects.
    /// Defaults to true, which is strictly required by the SeaweedFS architecture.
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use SSL/TLS (HTTPS) for requests.
    /// Defaults to false.
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the S3 health check is disabled or not.
    /// </summary>
    public bool DisableHealthChecks { get; set; } = false;

    /// <summary>
    /// Gets or sets an action to deeply configure the underlying AmazonS3Config (e.g., retries, telemetry, proxies).
    /// </summary>
    public Action<AmazonS3Config>? ConfigureS3Config { get; set; }

    internal void ParseConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        // If the connection string is an absolute URI (e.g., "http://...", "https://..."), use it as the endpoint.
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            Endpoint = uri;
            return;
        }

        try
        {
            var connectionBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (connectionBuilder.TryGetValue(ConnectionStringEndpoint, out var endpoint) &&
                Uri.TryCreate(endpoint.ToString()?.Trim(), UriKind.Absolute, out var serviceUri))
            {
                Endpoint = serviceUri;
            }

            if (connectionBuilder.TryGetValue(UseSslKey, out var useSslObj) &&
                useSslObj is string useSslString &&
                bool.TryParse(useSslString, out var useSslParsed))
            {
                UseSsl = useSslParsed;
            }

            if (connectionBuilder.TryGetValue(ConnectionStringFilerEndpoint, out var filerEndpoint) &&
                Uri.TryCreate(filerEndpoint.ToString()?.Trim(), UriKind.Absolute, out var filerServiceUri))
            {
                FilerEndpoint = filerServiceUri;
            }
            else if (connectionBuilder.TryGetValue(ConnectionStringFilerUrl, out var filerUrl) &&
                     Uri.TryCreate(filerUrl.ToString()?.Trim(), UriKind.Absolute, out var filerUrlUri))
            {
                FilerEndpoint = filerUrlUri;
            }

            if (connectionBuilder.TryGetValue(AccessKeyName, out var accessKeyValue) && accessKeyValue is string accessKey)
            {
                AccessKey = accessKey;
            }

            if (connectionBuilder.TryGetValue(SecretKeyName, out var secretKeyValue) && secretKeyValue is string secretKey)
            {
                SecretKey = secretKey;
            }
        }
        catch (ArgumentException)
        {
            // Ignore badly formed connection strings gracefully.
        }
    }
}