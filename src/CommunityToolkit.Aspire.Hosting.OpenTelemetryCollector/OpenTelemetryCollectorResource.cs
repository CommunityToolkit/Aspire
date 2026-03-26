using Aspire.Hosting.ApplicationModel;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Represents an OpenTelemetry Collector container resource.
/// </summary>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class OpenTelemetryCollectorResource(string name) : ContainerResource(name)
{
    internal static string GrpcEndpointName = "grpc";
    internal static string HttpEndpointName = "http";

    /// <summary>
    /// Gets the gRPC endpoint for the collector.
    /// </summary>
    public EndpointReference GrpcEndpoint => new(this, GrpcEndpointName);

    /// <summary>
    /// Gets the HTTP endpoint for the collector.
    /// </summary>
    public EndpointReference HttpEndpoint => new(this, HttpEndpointName);
}
