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
    /// Whether the server is distributed Chroma, the one Chroma Cloud runs on, installed elsewhere, like with the Helm chart of Chroma.
    /// The client then works as on distributed Chroma, as with <c>WithDistributedChroma</c> of ChromaDotNet.Client. An address of
    /// Chroma Cloud is distributed Chroma in any case.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool Distributed { get; set; }

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
    /// Sets <see cref="Endpoint"/>, <see cref="Token"/>, <see cref="Tenant"/> and <see cref="Database"/> when it has them, and
    /// <see cref="Distributed"/> when it says <c>Distributed=true</c>, from a connection string like
    /// <c>Endpoint=https://api.trychroma.com;Token=...;Tenant=...;Database=...</c>, or just a URI.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <exception cref="ArgumentException">The connection string has another key, no endpoint, or a <c>Distributed</c> that is not <c>true</c> or <c>false</c>.</exception>
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

        // Only Distributed=true of the connection string, not IsDistributedChroma, which is also true for an address of Chroma Cloud,
        // so that the setting does not stay when the code gives another Endpoint.
        if (options.DistributedChroma)
        {
            Distributed = true;
        }
    }
}
