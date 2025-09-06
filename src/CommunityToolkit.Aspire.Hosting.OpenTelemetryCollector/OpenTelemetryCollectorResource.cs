using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// The collector resource
/// </summary>
/// <param name="name">Name of the resource</param>
public class OpenTelemetryCollectorResource(string name) : ContainerResource(name)
{
    internal static string GrpcEndpointName = "grpc";
    internal static string HttpEndpointName = "http";

    /// <summary>
    /// gRPC Endpoint
    /// </summary>
    public EndpointReference GrpcEndpoint => new(this, GrpcEndpointName);

    /// <summary>
    /// HTTP Endpoint
    /// </summary>
    public EndpointReference HttpEndpoint => new(this, HttpEndpointName);
}
