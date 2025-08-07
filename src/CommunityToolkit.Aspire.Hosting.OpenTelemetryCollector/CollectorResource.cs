using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// The collector resource
/// </summary>
/// <param name="name">Name of the resource</param>
public class CollectorResource(string name) : ContainerResource(name)
{
    internal static string GRPCEndpointName = "grpc";
    internal static string HTTPEndpointName = "http";

    /// <summary>
    /// gRPC Endpoint
    /// </summary>
    public EndpointReference GRPCEndpoint => new(this, GRPCEndpointName);

    /// <summary>
    /// HTTP Endpoint
    /// </summary>
    public EndpointReference HTTPEndpoint => new(this, HTTPEndpointName);
}
