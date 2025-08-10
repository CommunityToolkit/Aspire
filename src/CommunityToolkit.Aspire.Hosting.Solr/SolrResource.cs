using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an Apache Solr container resource.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class SolrResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";

    private readonly List<SolrCoreResource> _cores = new();

    /// <summary>
    /// Gets the list of Solr cores associated with this resource.
    /// </summary>
    public IReadOnlyList<SolrCoreResource> Cores => _cores;

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Solr server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Solr server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{PrimaryEndpoint.Property(EndpointProperty.Url)}");

    /// <summary>
    /// Adds a Solr core to this resource.
    /// </summary>
    /// <param name="core">The core to add.</param>
    public void AddCore(SolrCoreResource core)
    {
        _cores.Add(core);
    }

    /// <summary>
    /// Gets the connection string for the Solr server.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A connection string for the Solr server.</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation))
        {
            return connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken);
        }

        return ConnectionStringExpression.GetValueAsync(cancellationToken);
    }
}

/// <summary>
/// Represents a Solr core resource.
/// </summary>
/// <param name="name">The name of the core.</param>
/// <param name="parent">The parent Solr resource.</param>
public class SolrCoreResource(string name, SolrResource parent) : Resource(name), IResourceWithConnectionString
{
    /// <summary>
    /// Gets the parent Solr resource.
    /// </summary>
    public SolrResource Parent { get; } = parent;

    /// <summary>
    /// Gets the connection string expression for this Solr core.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Parent.PrimaryEndpoint.Property(EndpointProperty.Url)}/solr/{Name}");

    /// <summary>
    /// Gets the connection string for this Solr core.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A connection string for this Solr core.</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation))
        {
            return connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken);
        }

        return ConnectionStringExpression.GetValueAsync(cancellationToken);
    }
}
