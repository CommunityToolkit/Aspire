namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a flagd container.
/// </summary>
/// <remarks>
/// Constructs a <see cref="FlagdResource"/>.
/// </remarks>
/// <param name="name">The name of the resource.</param>
public class FlagdResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HttpEndpointName = "http";
    internal const string HealthCheckEndpointName = "health";
    internal const string OfrepEndpointName = "ofrep";

    private EndpointReference? _primaryEndpointReference;

    private EndpointReference? _healthCheckEndpointReference;

    private EndpointReference? _ofrepEndpointReference;

    /// <summary>
    /// Gets the primary HTTP endpoint for the flagd server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpointReference ??= new(this, HttpEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the health check HTTP endpoint for the flagd server.
    /// </summary>
    public EndpointReference HealthCheckEndpoint => _healthCheckEndpointReference ??= new(this, HealthCheckEndpointName);

    /// <summary>
    /// Gets the connection string for the flagd server.
    /// </summary>
    public EndpointReference OfrepEndpoint => _ofrepEndpointReference ??= new(this, OfrepEndpointName);

    /// <summary>
    /// Gets the connection string expression for the flagd server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}"
        );

    /// <summary>
    /// Gets the connection URI expression for the flagd server.
    /// </summary>
    /// <remarks>
    /// Format: <c>http://{host}:{port}</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ConnectionStringExpression;

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}