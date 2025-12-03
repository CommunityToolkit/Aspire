using System.Data.Common;

namespace CommunityToolkit.Aspire.Hosting.Logto.Client;

/// <summary>
/// Provides utility methods for extracting and validating endpoint information
/// from connection strings in various formats. This helper is specifically designed
/// to assist with parsing connection strings for use with the Logto client configuration.
/// </summary>
public class LogtoConnectionStringHelper
{
    private const string ConnectionStringEndpointKey = "Endpoint";

    /// <summary>
    /// Retrieves the endpoint value from a given connection string. If the connection string is a valid URI,
    /// the method returns the URI as a string. If the connection string is in a key-value pair format,
    /// it extracts the value of the "Endpoint" key if present and validates it as a URI.
    /// </summary>
    /// <param name="connectionString">The connection string to parse for an endpoint.</param>
    /// <returns>
    /// A string representation of the endpoint if found and valid; otherwise, null if the connection
    /// string is null, empty, or does not contain a valid endpoint.
    /// </returns>
    public static string? GetEndpointFromConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }
        
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            return uri.ToString();
        }

        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        if (builder.TryGetValue(ConnectionStringEndpointKey, out var endpointObj) &&
            endpointObj is string endpoint &&
            Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            return endpointUri.ToString();
        }

        return null;
    }
}