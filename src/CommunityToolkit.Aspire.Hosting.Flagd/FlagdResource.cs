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

    private EndpointReference? _primaryEndpointReference;

    private EndpointReference? _healthCheckEndpointReference;

    /// <summary>
    /// Gets the primary HTTP endpoint for the flagd server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpointReference ??= new(this, HttpEndpointName);

    /// <summary>
    /// Gets the health check HTTP endpoint for the flagd server.
    /// </summary>
    public EndpointReference HealthCheckEndpoint => _healthCheckEndpointReference ??= new(this, HealthCheckEndpointName);

    /// <summary>
    /// Gets the connection string expression for the flagd server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}"
        );
}