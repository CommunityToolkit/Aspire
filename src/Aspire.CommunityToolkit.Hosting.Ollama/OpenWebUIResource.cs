namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an Open WebUI resource
/// </summary>
public class OpenWebUIResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the http endpoint for the Open WebUI resource.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Open WebUI endpoint.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
       ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Url)}");
}