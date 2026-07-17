namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a listmonk instance.
/// </summary>
[AspireExport(ExposeProperties = true)]
public class ListmonkResource([ResourceName] string name) : ContainerResource(name), IResourceWithServiceDiscovery, IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";

    /// <summary>
    /// Gets the primary HTTP endpoint for the listmonk instance.
    /// </summary>
    public EndpointReference PrimaryEndpoint => new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the connection string expression for the listmonk instance.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => UriExpression;

    /// <summary>
    /// Gets the connection URI expression for the listmonk instance.
    /// </summary>
    /// <remarks>
    /// Format: <c>http://{host}:{port}</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"http://{Host}:{Port}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}
