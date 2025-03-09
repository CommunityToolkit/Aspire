namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a RavenDB container.
/// </summary>
public class RavenDBServerResource(string name, bool isSecured) : ContainerResource(name), IResourceWithConnectionString
{
    /// <summary>
    /// Indicates whether the server connection is secured (HTTPS) or not (HTTP).
    /// </summary>
    private bool IsSecured { get; } = isSecured;

    /// <summary>
    /// Gets the protocol used for the primary endpoint, based on the security setting ("http" or "https").
    /// </summary>
    internal string PrimaryEndpointName => IsSecured ? "https" : "http";
    /// <summary>
    /// Gets the name for the TCP Endpoint
    /// </summary>
    internal string TcpEndpointName = "tcp";

    private EndpointReference? _primaryEndpoint;
    private EndpointReference? tcpEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the RavenDB server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the TCP endpoint for the RavenDB server.
    /// </summary>
    public EndpointReference TcpEndpoint => tcpEndpoint ??= new(this, TcpEndpointName);

    /// <summary>
    /// Gets the connection string expression for the RavenDB server, 
    /// formatted as "http(s)://{Host}:{Port}" depending on the security setting.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
        $"URL={(IsSecured ? "https://" : "http://")}{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

    private readonly Dictionary<string, string> _databases = new();

    /// <summary>
    /// Gets a read-only dictionary of databases associated with this server resource.
    /// The key represents the resource name, and the value represents the database name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Databases => _databases;

    /// <summary>
    /// Adds a database to the resource.
    /// </summary>
    /// <param name="name">The name of the resource to associate with the database.</param>
    /// <param name="databaseName">The name of the database to add.</param>
    internal void AddDatabase(string name, string databaseName)
    {
        _databases.TryAdd(name, databaseName);
    }
}
