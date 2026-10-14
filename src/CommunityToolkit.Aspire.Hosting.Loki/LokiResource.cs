#pragma warning disable ASPIREATS001

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Grafana Loki container resource.
/// </summary>
/// <param name="name">The resource name.</param>
[AspireExport(ExposeProperties = true)]
public class LokiResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HttpEndpointName = "http";
    internal const string GrpcEndpointName = "grpc";

    private EndpointReference? _primaryEndpoint;
    private EndpointReference? _grpcEndpoint;

    /// <summary>
    /// Gets the HTTP endpoint used to send and query logs.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, HttpEndpointName);

    /// <summary>
    /// Gets the gRPC endpoint.
    /// </summary>
    public EndpointReference GrpcEndpoint => _grpcEndpoint ??= new(this, GrpcEndpointName);

    /// <summary>
    /// Gets the HTTP host expression.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the HTTP port expression.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the HTTP URI expression.
    /// </summary>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"{PrimaryEndpoint}");

    /// <summary>
    /// Gets the connection string as an HTTP URI.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => UriExpression;

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}

#pragma warning restore ASPIREATS001