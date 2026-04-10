namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a containerized llama.cpp server resource that can be added to a distributed
/// Aspire application. The resource exposes an HTTP endpoint used to communicate with the
/// running llama server and provides connection properties for consumers.
/// </summary>
public class LlamaCppServerResource: ContainerResource, IResourceWithConnectionString
{
    internal const string LlamaServerEndpointName = "http";
    internal string ModelName = string.Empty;
    internal List<string> EnvironmentArgs = [];
    internal string? VolumeName;

    private readonly EndpointReference _primaryEndpoint;

    /// <summary>
    /// Initializes a new instance of <see cref="LlamaCppServerResource"/> with the specified resource name.
    /// </summary>
    /// <param name="name">The logical name of the resource.</param>
    public LlamaCppServerResource(string name) : base(name)
    {
        _primaryEndpoint = new(this, LlamaServerEndpointName);
    }

    /// <summary>
    /// Gets an expression referencing the host of the HTTP endpoint exposed by the llama server.
    /// </summary>
    public EndpointReferenceExpression Host => _primaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets an expression referencing the port of the HTTP endpoint exposed by the llama server.
    /// </summary>
    public EndpointReferenceExpression Port => _primaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets a reference expression that resolves to a connection string that can be used
    /// to connect to the llama server (includes scheme, host and port).
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
      ReferenceExpression.Create(
        $"Endpoint={_primaryEndpoint.Property(EndpointProperty.Scheme)}://{_primaryEndpoint.Property(EndpointProperty.Host)}:{_primaryEndpoint.Property(EndpointProperty.Port)}"
     );

    /// <summary>
    /// Gets an expression that resolves to the public URI of the llama server (scheme://host:port).
    /// </summary>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"{_primaryEndpoint.Property(EndpointProperty.Scheme)}://{Host}:{Port}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}
