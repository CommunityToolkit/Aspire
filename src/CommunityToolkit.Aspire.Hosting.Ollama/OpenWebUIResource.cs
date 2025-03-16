namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an Open WebUI resource
/// </summary>
public class OpenWebUIResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the http endpoint for the Open WebUI resource.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Open WebUI endpoint.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
       ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Url)}");

    private readonly List<OllamaResource> ollamaResources = [];

    /// <summary>
    /// Gets the list of Ollama resources that are associated with this Open WebUI resource.
    /// </summary>
    public IReadOnlyList<OllamaResource> OllamaResources => ollamaResources;

    /// <summary>
    /// Adds an Ollama resource to the Open WebUI resource.
    /// </summary>
    /// <param name="ollamaResource">The Ollama resource to add.</param>
    /// <exception cref="InvalidOperationException">Thrown if the resource is already added.</exception>
    internal void AddOllamaResource(OllamaResource ollamaResource)
    {
        if (ollamaResources.Contains(ollamaResource))
        {
            throw new InvalidOperationException($"The resource {ollamaResource.Name} is already added to the OpenWebUI resource.");
        }

        ollamaResources.Add(ollamaResource);
    }
}