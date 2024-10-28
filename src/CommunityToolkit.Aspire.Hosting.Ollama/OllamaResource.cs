using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an Ollama container.
/// </summary>
/// <remarks>
/// Constructs an <see cref="OllamaResource"/>.
/// </remarks>
/// <param name="name">The name for the resource.</param>
public class OllamaResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string OllamaEndpointName = "http";

    private readonly List<string> _models = [];

    private EndpointReference? _primaryEndpointReference;

    /// <summary>
    /// Adds a model to the list of models to download on initial startup.
    /// </summary>
    public IReadOnlyList<string> Models => _models;

    /// <summary>
    /// Gets the endpoint for the Ollama server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpointReference ??= new(this, OllamaEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Ollama server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
      ReferenceExpression.Create(
        $"Endpoint={PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}"
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
}