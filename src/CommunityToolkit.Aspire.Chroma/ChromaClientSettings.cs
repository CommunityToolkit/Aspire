using System.Data.Common;

namespace CommunityToolkit.Aspire.Chroma;

/// <summary>
/// Provides the client configuration settings for connecting to a ChromaDB server using ChromaClient.
/// </summary>
public sealed class ChromaClientSettings
{
    private const string ConnectionStringEndpoint = "Endpoint";

    /// <summary>
    /// The endpoint URI string of the ChromaDB server to connect to.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the ChromaDB health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets a integer value that indicates the ChromaDB health check timeout in milliseconds.
    /// </summary>
    public int? HealthCheckTimeout { get; set; }

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

            if (connectionBuilder.TryGetValue(ConnectionStringEndpoint, out var endpoint) && Uri.TryCreate(endpoint.ToString(), UriKind.Absolute, out var serviceUri))
            {
                Endpoint = serviceUri;
            }
        }
    }
}
