#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a container resource for dbx.
/// </summary>
/// <param name="name">The name of the container resource.</param>
[AspireExport(ExposeProperties = true)]
public sealed class DbxContainerResource(string name) : ContainerResource(name)
{
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the dbx resource.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    internal ICollection<DbxConnectionConfig> Connections { get; set; } = new List<DbxConnectionConfig>();

    /// <summary>
    /// Adds a new connection to the dbx container resource.
    /// </summary>
    /// <param name="connection">The connection to add</param>
    /// <returns>Returns <c>false</c> if the connection was already added.</returns>
    public bool AddConnection(DbxConnectionConfig connection)
    {
        if (Connections.Any(c => c.Id == connection.Id))
        {
            return false;
        }
        
        Connections.Add(connection);
        return true;
    }
}

#pragma warning restore ASPIREATS001
