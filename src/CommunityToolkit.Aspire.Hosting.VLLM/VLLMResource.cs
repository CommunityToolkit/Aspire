#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a <see href="https://github.com/vllm-project/vllm">vLLM</see>
/// OpenAI-compatible inference server container.
/// </summary>
/// <param name="name">The name for the resource.</param>
[AspireExport(ExposeProperties = true)]
public class VLLMResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    /// <summary>
    /// The name of the HTTP endpoint that exposes the vLLM API.
    /// </summary>
    internal const string PrimaryEndpointName = "http";

    /// <summary>
    /// The default HTTP port inside the vLLM container.
    /// </summary>
    internal const int DefaultContainerPort = 8000;

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets or sets the model resource served by this server.
    /// </summary>
    internal VLLMModelResource? Model { get; set; }

    /// <summary>
    /// Gets or sets the name exposed by the server for the model.
    /// </summary>
    internal string? ServedModelName { get; set; }

    /// <summary>
    /// Gets the primary HTTP endpoint that exposes the OpenAI-compatible API.
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
    /// Gets the connection string expression for the vLLM server.
    /// </summary>
    /// <remarks>
    /// The OpenAI-compatible API is served under the <c>/v1</c> path of this endpoint.
    /// </remarks>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"Endpoint={PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{Host}:{Port}");

    /// <summary>
    /// Gets the URI expression for the vLLM server.
    /// </summary>
    public ReferenceExpression UriExpression =>
        ReferenceExpression.Create($"{PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{Host}:{Port}");

    /// <inheritdoc />
    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}
