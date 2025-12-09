namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an OpenFGA container.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class OpenFgaResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HttpEndpointName = "http";
    internal const string GrpcEndpointName = "grpc";

    private EndpointReference? _httpEndpoint;
    private EndpointReference? _grpcEndpoint;

    /// <summary>
    /// Gets the HTTP endpoint for the OpenFGA server.
    /// </summary>
    public EndpointReference HttpEndpoint => _httpEndpoint ??= new(this, HttpEndpointName);

    /// <summary>
    /// Gets the gRPC endpoint for the OpenFGA server.
    /// </summary>
    public EndpointReference GrpcEndpoint => _grpcEndpoint ??= new(this, GrpcEndpointName);

    /// <summary>
    /// Gets the connection string expression for the OpenFGA HTTP endpoint.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{HttpEndpoint.Property(EndpointProperty.Scheme)}://{HttpEndpoint.Property(EndpointProperty.Host)}:{HttpEndpoint.Property(EndpointProperty.Port)}"
        );
}
