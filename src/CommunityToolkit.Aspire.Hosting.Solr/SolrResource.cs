using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an Apache Solr container resource.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="coreName">The name of the Solr core.</param>
public class SolrResource(string name, string coreName) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// The Solr core name.
    /// </summary>
    public string CoreName { get; set; } = coreName;

    /// <summary>
    /// Gets the primary endpoint for the Solr server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Solr server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>  ReferenceExpression.Create(
            $"http://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}/solr/{CoreName}");

}