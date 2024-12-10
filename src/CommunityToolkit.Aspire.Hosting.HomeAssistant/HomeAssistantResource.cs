namespace Aspire.Hosting;

/// <summary>
/// A resource that represents an HomeAssistant container.
/// </summary>
/// <remarks>
/// Constructs an <see cref="HomeAssistantResource"/>.
/// </remarks>
/// <param name="name">The name for the resource.</param>
public class HomeAssistantResource([ResourceName] string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HomeAssistantEndpointName = "http";

    private EndpointReference? _primaryEndpointReference;

    /// <summary>
    /// Gets the endpoint for the Ollama server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpointReference ??= new(this, HomeAssistantEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Ollama server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
      ReferenceExpression.Create(
        $"Endpoint={PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}"
      );
}
