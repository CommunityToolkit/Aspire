using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// A resource that represents the Grafana OTel-LGTM observability stack container.
/// </summary>
/// <remarks>
/// The <c>grafana/otel-lgtm</c> Docker image bundles the OpenTelemetry Collector,
/// Prometheus, Loki, Tempo, Pyroscope, and Grafana into a single container for
/// development, demo, and testing environments.
/// </remarks>
/// <param name="name">The name of the resource.</param>
public class GrafanaOtelLgtmResource(string name) : ContainerResource(name)
{
    internal const string GrafanaEndpointName = "grafana";
    internal const string OtlpGrpcEndpointName = "otel-grpc";
    internal const string OtlpHttpEndpointName = "otel-http";
    internal const string PrometheusEndpointName = "prometheus";
    internal const string PyroscopeEndpointName = "pyroscope";

    /// <summary>
    /// Gets the Grafana web UI endpoint (port 3000).
    /// </summary>
    public EndpointReference GrafanaEndpoint => new(this, GrafanaEndpointName);

    /// <summary>
    /// Gets the OpenTelemetry Collector gRPC endpoint (port 4317).
    /// </summary>
    public EndpointReference OtlpGrpcEndpoint => new(this, OtlpGrpcEndpointName);

    /// <summary>
    /// Gets the OpenTelemetry Collector HTTP endpoint (port 4318).
    /// </summary>
    public EndpointReference OtlpHttpEndpoint => new(this, OtlpHttpEndpointName);

    /// <summary>
    /// Gets the Prometheus HTTP endpoint (port 9090).
    /// </summary>
    public EndpointReference PrometheusEndpoint => new(this, PrometheusEndpointName);

    /// <summary>
    /// Gets the Pyroscope HTTP endpoint (port 4040).
    /// </summary>
    public EndpointReference PyroscopeEndpoint => new(this, PyroscopeEndpointName);
}
