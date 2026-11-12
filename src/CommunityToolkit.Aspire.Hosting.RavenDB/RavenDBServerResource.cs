using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;
using CommunityToolkit.Aspire.Hosting.RavenDB;

#pragma warning disable ASPIREATS001 // AspireExport is experimental
#pragma warning disable CTASPIRERAVENDB001 // The deployment packages' API is this package's own

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a RavenDB container.
/// </summary>
[AspireExport(ExposeProperties = true)]
public class RavenDBServerResource(string name, bool isSecured) : ContainerResource(name), IResourceWithConnectionString
{
    /// <summary>
    /// Indicates whether the server connection is secured (HTTPS) or not (HTTP).
    /// </summary>
    internal bool IsSecured { get; } = isSecured;

    /// <summary>
    /// Gets the protocol used for the primary endpoint, based on the security setting ("http" or "https").
    /// </summary>
    internal string PrimaryEndpointName => IsSecured ? "https" : "http";
    /// <summary>
    /// Gets the name for the TCP Endpoint
    /// </summary>
    internal string TcpEndpointName = "tcp";

    /// <summary>
    /// The public server URL (domain) configured for this resource.
    /// </summary>
    internal string? PublicServerUrl { get; init; }

    /// <summary>
    /// Optional client certificate used by hosting code (health checks, database
    /// creation, etc.) when connecting to this RavenDB server in secured setups.
    /// </summary>
    internal X509Certificate2? ClientCertificate { get; init; }

    private EndpointReference? _primaryEndpoint;
    private EndpointReference? tcpEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the RavenDB server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the TCP endpoint for the RavenDB server.
    /// </summary>
    public EndpointReference TcpEndpoint => tcpEndpoint ??= new(this, TcpEndpointName);

    /// <summary>
    /// Gets the connection string expression for the RavenDB server, 
    /// formatted as "http(s)://{Host}:{Port}" depending on the security setting.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            if (PublishStrategy is { } strategy)
                return ReferenceExpression.Create($"URL={strategy.Url}");

            if (IsSecured && !string.IsNullOrEmpty(PublicServerUrl))
                return ReferenceExpression.Create($"URL={PublicServerUrl}");

            return ReferenceExpression.Create(
                $"URL={(IsSecured ? "https://" : "http://")}{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");
        }
    }

    /// <summary>
    /// Gets the connection URI expression for the RavenDB server.
    /// </summary>
    /// <remarks>
    /// Format: <c>http://{host}:{port}</c> or <c>https://{host}:{port}</c> depending on security settings.
    /// </remarks>
    public ReferenceExpression UriExpression =>
        PublishStrategy?.Url ?? ReferenceExpression.Create($"{(IsSecured ? "https://" : "http://")}{Host}:{Port}");

    /// <summary>
    /// For deployment packages: the strategy that publishes the server somewhere else than its container, such as
    /// RavenDB Cloud. <see langword="null"/> means the container itself is published.
    /// </summary>
    /// <remarks>
    /// While it is set, the connection string and <see cref="UriExpression"/> point at the strategy's URL. The endpoint
    /// properties (<see cref="PrimaryEndpoint"/>, <see cref="Host"/>, <see cref="Port"/>) still describe the local
    /// container, which <c>aspire run</c> starts.
    /// </remarks>
    [Experimental("CTASPIRERAVENDB001")]
    [AspireExportIgnore(Reason = "For deployment packages, not AppHosts.")]
    public RavenDBPublishStrategy? PublishStrategy { get; private set; }

    /// <summary>
    /// Publishes the server with <paramref name="strategy"/> instead of its container. A server has one strategy.
    /// </summary>
    internal void UsePublishStrategy(RavenDBPublishStrategy strategy)
    {
        if (PublishStrategy is { } existing)
        {
            throw new DistributedApplicationException(
                $"RavenDB server '{Name}' is already published with {existing.Name}, so it cannot also be published with " +
                $"{strategy.Name}. A server has one publish strategy; to use a different one per environment, choose it " +
                "with an if in the AppHost.");
        }

        PublishStrategy = strategy;
    }

    private readonly Dictionary<string, string> _databases = new();
    private readonly HashSet<string> _databasesToCreate = new(StringComparer.Ordinal);

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
    /// <param name="ensureCreated">Whether the database is created when the server starts.</param>
    internal void AddDatabase(string name, string databaseName, bool ensureCreated = false)
    {
        _databases.TryAdd(name, databaseName);

        if (ensureCreated)
        {
            _databasesToCreate.Add(databaseName);
        }
    }

    /// <summary>
    /// For deployment packages: the names of the databases declared with <c>ensureCreated: true</c>. The AppHost creates
    /// them in run mode; a publish strategy that deploys the server creates them where it deploys it.
    /// </summary>
    [Experimental("CTASPIRERAVENDB001")]
    [AspireExportIgnore(Reason = "For deployment packages, not AppHosts.")]
    public IReadOnlyCollection<string> DatabasesToCreate => _databasesToCreate;

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        if (PublishStrategy is { } strategy)
        {
            // Host and port of a server published elsewhere are not known separately.
            yield return new("Uri", strategy.Url);
            yield break;
        }

        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}
