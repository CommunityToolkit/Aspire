using ChromaDB.Client;

namespace CommunityToolkit.Aspire.Chroma;

/// <summary>
/// Provides the client configuration settings for connecting to a ChromaDB server using ChromaClient.
/// </summary>
public sealed class ChromaClientSettings
{
    /// <summary>
    /// The endpoint URI string of the ChromaDB server to connect to.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// The token sent in the <c>X-Chroma-Token</c> header of each request, like an API key of Chroma Cloud.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// The tenant of the requests. When it is not set, <c>default_tenant</c>.
    /// </summary>
    public string? Tenant { get; set; }

    /// <summary>
    /// The database of the requests. When it is not set, <c>default_database</c>.
    /// </summary>
    public string? Database { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the ChromaDB health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets an integer value that indicates the ChromaDB health check timeout in milliseconds.
    /// </summary>
    public int? HealthCheckTimeout { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the OpenTelemetry tracing is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableTracing { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the OpenTelemetry metrics are disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableMetrics { get; set; }

    /// <summary>
    /// Sets <see cref="Endpoint"/>, and <see cref="Token"/>, <see cref="Tenant"/> and <see cref="Database"/> when it has them,
    /// from a connection string like <c>Endpoint=https://api.trychroma.com;Token=...;Tenant=...;Database=...</c>, or just a URI.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <exception cref="ArgumentException">The connection string has another key, or no endpoint.</exception>
    internal void ParseConnectionString(string connectionString)
    {
        var options = ChromaConfigurationOptions.FromConnectionString(connectionString);
        Endpoint = options.Uri;

        if (!string.IsNullOrEmpty(options.ChromaToken))
        {
            Token = options.ChromaToken;
        }

        if (!string.IsNullOrEmpty(options.Tenant))
        {
            Tenant = options.Tenant;
        }

        if (!string.IsNullOrEmpty(options.Database))
        {
            Database = options.Database;
        }
    }
}
