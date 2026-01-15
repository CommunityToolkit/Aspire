using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Permify;

/// <summary>
/// Resource for the Permify API server.
/// </summary>
public sealed class PermifyResource(string name) : ContainerResource(name)
{
    /// <summary>
    /// The name of the HTTP API endpoint.
    /// </summary>
    public const string HttpsEndpointName = "https";

    /// <summary>
    /// The name of the gRPC API endpoint.
    /// </summary>
    public const string GrpcEndpointName = "grpc";
}