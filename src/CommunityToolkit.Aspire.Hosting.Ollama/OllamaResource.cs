namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an Ollama container.
/// </summary>
/// <remarks>
/// Constructs an <see cref="OllamaResource"/>.
/// </remarks>
/// <param name="name">The name for the resource.</param>
/// <param name="modelName">The LLM to download on initial startup.</param>
public class OllamaResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string OllamaEndpointName = "http";

    private readonly List<string> _models = [];

    private string? _defaultModel = null;

    private EndpointReference? _primaryEndpointReference;

    /// <summary>
    /// Adds a model to the list of models to download on initial startup.
    /// </summary>
    public IReadOnlyList<string> Models => _models;

    /// <summary>
    /// The default model to be configured on the Ollama server.
    /// </summary>
    public string? DefaultModel => _defaultModel;

    /// <summary>
    /// Gets the endpoint for the Ollama server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpointReference ??= new(this, OllamaEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Ollama server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
      ReferenceExpression.Create(
        $"{PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}"
      );

    /// <summary>
    ///     Adds a model to the list of models to download on initial startup.
    /// </summary>
    /// <param name="modelName">The name of the model</param>
    public void AddModel(string modelName)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelName, nameof(modelName));
        if (!_models.Contains(modelName))
        {
            _models.Add(modelName);
        }
    }

    /// <summary>
    /// Sets the default model to be configured on the Ollama server.
    /// </summary>
    /// <param name="modelName">The name of the model.</param>
    /// <remarks>
    /// If the model does not exist in the list of models, it will be added.
    /// </remarks>
    public void SetDefaultModel(string modelName)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelName, nameof(modelName));
        _defaultModel = modelName;

        if (!_models.Contains(modelName))
        {
            AddModel(modelName);
        }
    }
}