namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an Ollama container.
/// </summary>
/// <remarks>
/// Constructs an <see cref="OllamaResource"/>.
/// </remarks>
/// <param name="name">The name for the resource.</param>
/// <param name="modelName">The LLM to download on initial startup.</param>
public class OllamaResource(string name, string modelName) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string OllamaEndpointName = "ollama";

    private EndpointReference? _endpointReference;

    public string ModelName { get; internal set; } = modelName;

    /// <summary>
    /// Gets the endpoint for the Ollama server.
    /// </summary>
    public EndpointReference Endpoint => _endpointReference ??= new(this, OllamaEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Ollama server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
      ReferenceExpression.Create(
        $"http://{Endpoint.Property(EndpointProperty.Host)}:{Endpoint.Property(EndpointProperty.Port)}"
      );
}
