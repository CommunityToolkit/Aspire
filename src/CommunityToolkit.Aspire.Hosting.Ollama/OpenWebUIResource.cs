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
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the connection string expression for the Open WebUI endpoint.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
       ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Url)}");

    /// <summary>
    /// Gets the connection URI expression for the Open WebUI endpoint.
    /// </summary>
    /// <remarks>
    /// Format: <c>http://{host}:{port}</c>. The scheme reflects the endpoint configuration and may be <c>https</c> when TLS is enabled.
    /// </remarks>
    public ReferenceExpression UriExpression => ConnectionStringExpression;

    private readonly List<IOllamaResource> ollamaResources = [];

    /// <summary>
    /// Gets the list of Ollama resources that are associated with this Open WebUI resource.
    /// </summary>
    public IReadOnlyList<IOllamaResource> OllamaResources => ollamaResources;

    /// <summary>
    /// Adds an Ollama resource to the Open WebUI resource.
    /// </summary>
    /// <param name="ollamaResource">The Ollama resource to add.</param>
    /// <exception cref="InvalidOperationException">Thrown if the resource is already added.</exception>
    internal void AddOllamaResource(IOllamaResource ollamaResource)
    {
        if (ollamaResources.Contains(ollamaResource))
        {
            throw new InvalidOperationException($"The resource {ollamaResource.Name} is already added to the OpenWebUI resource.");
        }

        ollamaResources.Add(ollamaResource);
    }

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}