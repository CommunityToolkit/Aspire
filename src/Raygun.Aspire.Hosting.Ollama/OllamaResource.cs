namespace Aspire.Hosting.ApplicationModel
{
  /// <summary>
  /// A resource that represents an Ollama container.
  /// </summary>
  public class OllamaResource : ContainerResource, IResourceWithConnectionString
  {
    internal const string OllamaEndpointName = "ollama";

    private EndpointReference? _endpointReference;

    /// <summary>
    /// Constructs an <see cref="OllamaResource"/>.
    /// </summary>
    /// <param name="name">The name for the resource.</param>
    /// <param name="modelName">The LLM to download on initial startup.</param>
    public OllamaResource(string name, string modelName)
      : base(name)
    {
      ModelName = modelName;
    }

    public string ModelName { get; internal set; }

    /// <summary>
    /// Gets the endpoint for the Ollama server.
    /// </summary>
    public EndpointReference Endpoint =>
      _endpointReference ??= new EndpointReference(this, OllamaEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Ollama server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
      ReferenceExpression.Create(
        $"http://{Endpoint.Property(EndpointProperty.Host)}:{Endpoint.Property(EndpointProperty.Port)}"
      );
  }
}
