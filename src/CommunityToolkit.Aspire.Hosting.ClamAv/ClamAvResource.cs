namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a ClamAv antivirus scanner
/// </summary>
public class ClamAvResource(string name) : ContainerResource(name), IResourceWithConnectionString
{

    internal const string PrimaryEndpointName = "tcp";
    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the ClamAv. This endpoint is used for all API calls over HTTP.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <inheritdoc/>
    public ReferenceExpression ConnectionStringExpression =>
    ReferenceExpression.Create(
        $"tcp://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}"
    );

}
