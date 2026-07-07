#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a listmonk instance.
/// </summary>
[AspireExport(ExposeProperties = true)]
public class ListmonkResource : ContainerResource, IResourceWithServiceDiscovery, IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";

    /// <summary>
    /// Initializes a new instance of the <see cref="ListmonkResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    public ListmonkResource([ResourceName] string name) : base(name)
    {
        PrimaryEndpoint = new(this, PrimaryEndpointName);
    }

    /// <summary>
    /// Gets the primary HTTP endpoint for the listmonk instance.
    /// </summary>
    public EndpointReference PrimaryEndpoint { get; }

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

#pragma warning restore ASPIREATS001 // AspireExport is experimental
