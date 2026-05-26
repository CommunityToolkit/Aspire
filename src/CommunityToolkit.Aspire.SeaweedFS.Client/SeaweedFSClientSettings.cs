using Amazon.S3;
using System.Data.Common;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.Hosting;
#pragma warning restore IDE0130

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
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out Uri? uri))
        {
            Endpoint = uri;
            return;
        }

        try
        {
            DbConnectionStringBuilder connectionBuilder = new()
            {
                ConnectionString = connectionString
            };

            if (connectionBuilder.TryGetValue(ConnectionStringEndpoint, out object? endpoint) &&
                Uri.TryCreate(endpoint.ToString()?.Trim(), UriKind.Absolute, out Uri? serviceUri))
            {
                Endpoint = serviceUri;
            }

            if (connectionBuilder.TryGetValue(UseSslKey, out object? useSslObj) &&
                useSslObj is string useSslString &&
                bool.TryParse(useSslString, out bool useSslParsed))
            {
                UseSsl = useSslParsed;
            }

            if (connectionBuilder.TryGetValue(ConnectionStringFilerEndpoint, out object? filerEndpoint) &&
                Uri.TryCreate(filerEndpoint.ToString()?.Trim(), UriKind.Absolute, out Uri? filerServiceUri))
            {
                FilerEndpoint = filerServiceUri;
            }
            else if (connectionBuilder.TryGetValue(ConnectionStringFilerUrl, out object? filerUrl) &&
                     Uri.TryCreate(filerUrl.ToString()?.Trim(), UriKind.Absolute, out Uri? filerUrlUri))
            {
                FilerEndpoint = filerUrlUri;
            }

            if (connectionBuilder.TryGetValue(AccessKeyName, out object? accessKeyValue) && accessKeyValue is string accessKey)
            {
                AccessKey = accessKey;
            }

            if (connectionBuilder.TryGetValue(SecretKeyName, out object? secretKeyValue) && secretKeyValue is string secretKey)
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