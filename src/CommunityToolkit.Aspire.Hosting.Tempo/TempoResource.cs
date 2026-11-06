namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Grafana Tempo container resource.
/// </summary>
/// <param name="name">The resource name.</param>
[AspireExport(ExposeProperties = true)]
public class TempoResource(string name) : ContainerResource(name), IResourceWithServiceDiscovery
{
    internal const string HttpEndpointName = "http";
    internal const string GrpcEndpointName = "grpc";
    internal const string OtlpGrpcEndpointName = "otlp-grpc";
    internal const string OtlpHttpEndpointName = "otlp-http";

    private EndpointReference? _httpEndpoint;
    private EndpointReference? _grpcEndpoint;
    private EndpointReference? _otlpGrpcEndpoint;
    private EndpointReference? _otlpHttpEndpoint;

    /// <summary>
    /// Gets the HTTP endpoint used to query traces and check readiness.
    /// </summary>
    public EndpointReference HttpEndpoint => _httpEndpoint ??= new(this, HttpEndpointName);

    /// <summary>
    /// Gets the internal Tempo gRPC endpoint, not the OTLP ingestion endpoint.
    /// </summary>
    public EndpointReference GrpcEndpoint => _grpcEndpoint ??= new(this, GrpcEndpointName);

    /// <summary>
    /// Gets the OTLP gRPC endpoint used to ingest traces.
    /// </summary>
    public EndpointReference OtlpGrpcEndpoint => _otlpGrpcEndpoint ??= new(this, OtlpGrpcEndpointName);

    /// <summary>
    /// Gets the OTLP HTTP endpoint used to ingest traces.
    /// </summary>
    public EndpointReference OtlpHttpEndpoint => _otlpHttpEndpoint ??= new(this, OtlpHttpEndpointName);
}