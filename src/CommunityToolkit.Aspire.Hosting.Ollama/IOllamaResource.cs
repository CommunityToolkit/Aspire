namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an Ollama resource.
/// </summary>
public interface IOllamaResource : IResourceWithConnectionString, IResourceWithEndpoints
{
    /// <summary>
    /// Gets the list of models to download on initial startup.
    /// </summary>
    IReadOnlyList<string> Models { get; }

    /// <summary>
    /// Gets the endpoint for the Ollama server.
    /// </summary>
    EndpointReference PrimaryEndpoint { get; }
    
    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    EndpointReferenceExpression Host { get; }
    
    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    EndpointReferenceExpression Port { get; }
    
    /// <summary>
    /// Gets the connection URI expression for the Ollama server.
    /// </summary>
    /// <remarks>
    /// Format: <c>http://{host}:{port}</c>.
    /// </remarks>
    ReferenceExpression UriExpression { get; }
    
    /// <summary>
    /// Adds a model to the list of models to download on initial startup.
    /// </summary>
    /// <param name="modelName">The name of the model</param>
    void AddModel(string modelName);
}