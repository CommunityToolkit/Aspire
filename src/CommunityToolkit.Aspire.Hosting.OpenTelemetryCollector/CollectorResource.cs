using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector;

/// <summary>
/// Represents an OpenTelemetry Collector resource
/// </summary>
/// <param name="name"></param>
public class CollectorResource(string name) : ContainerResource(name)
{
    internal static string GRPCEndpointName = "grpc";
    internal static string HTTPEndpointName = "http";

    internal EndpointReference GRPCEndpoint => new(this, GRPCEndpointName);

    internal EndpointReference HTTPEndpoint => new(this, HTTPEndpointName);
}
