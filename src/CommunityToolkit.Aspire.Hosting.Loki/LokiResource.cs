namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Grafana Loki container resource.
/// </summary>
/// <param name="name">The resource name.</param>
[AspireExport(ExposeProperties = true)]
public class LokiResource(string name) : ContainerResource(name), IResourceWithServiceDiscovery
{
    internal const string HttpEndpointName = "http";
    internal const string GrpcEndpointName = "grpc";

    private EndpointReference? _httpEndpoint;
    private EndpointReference? _grpcEndpoint;

    /// <summary>
    /// Gets the HTTP endpoint used to send and query logs.
    /// </summary>
    public EndpointReference HttpEndpoint => _httpEndpoint ??= new(this, HttpEndpointName);

    /// <summary>
    /// Gets the gRPC endpoint.
    /// </summary>
    public EndpointReference GrpcEndpoint => _grpcEndpoint ??= new(this, GrpcEndpointName);
}
