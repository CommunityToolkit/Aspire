namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Meilisearch UI
/// </summary>
/// <param name="name">The name of the resource.</param>
public class MeilisearchResourceUI(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the http endpoint for the Meilisearch UI resource.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Meilisearch UI endpoint.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
       ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Url)}");
}

