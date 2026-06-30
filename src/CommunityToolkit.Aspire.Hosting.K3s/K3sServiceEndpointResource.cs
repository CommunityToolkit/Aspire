#pragma warning disable ASPIREATS001 // AspireExport is experimental

using CommunityToolkit.Aspire.Hosting;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Kubernetes Service exposed from a k3s cluster as an Aspire endpoint resource.
/// </summary>
/// <remarks>
/// An in-process WebSocket port-forward bridges the cluster service to a host port when the
/// cluster is ready. Dependent resources receive the service URL via the standard Aspire
/// service-discovery environment variable <c>services__{name}__url</c>:
/// <list type="bullet">
///   <item>Host processes receive <c>http(s)://localhost:{port}</c>.</item>
///   <item>Containers receive <c>http(s)://host.docker.internal:{port}</c>.</item>
/// </list>
/// Use <c>WaitFor(endpoint)</c> on dependent resources to ensure the port-forward is active
/// before they start.
/// </remarks>
[AspireExport(ExposeProperties = true)]
public sealed class K3sServiceEndpointResource(
    string name,
    string serviceName,
    int servicePort,
    string @namespace,
    K3sClusterResource cluster)
    : Resource(name), IResourceWithParent<K3sClusterResource>, IResourceWithWaitSupport,
      IResourceWithConnectionString
{
    /// <inheritdoc />
    public K3sClusterResource Parent { get; } = cluster ?? throw new ArgumentNullException(nameof(cluster));

    /// <summary>Gets the name of the Kubernetes Service being forwarded.</summary>
    public string ServiceName { get; } = serviceName ?? throw new ArgumentNullException(nameof(serviceName));

    /// <summary>Gets the port number declared on the Kubernetes Service.</summary>
    public int ServicePort { get; } = servicePort;

    /// <summary>Gets the Kubernetes namespace that contains the Service.</summary>
    public string Namespace { get; } = @namespace ?? throw new ArgumentNullException(nameof(@namespace));

    /// <summary>
    /// Gets the host port bound by the port-forward listener.
    /// </summary>
    /// <remarks>
    /// Zero until the resource transitions to <c>Running</c>. Use <c>WaitFor(endpoint)</c>
    /// to ensure this is populated before reading it from a dependent resource.
    /// </remarks>
    public int HostPort { get; internal set; }

    /// <summary>
    /// The URL scheme used for <c>services__{name}__url</c> injection and dashboard URLs.
    /// Set by <c>AddServiceEndpoint</c>; callers can override the default port-based inference
    /// via the <c>scheme</c> parameter.
    /// </summary>
    internal string Scheme { get; init; } = "http";

    /// <summary>
    /// <see langword="true"/> when the port-forward is active and accepting connections.
    /// Set by <c>K3sInProcessPortForwarder</c>; read by the health check.
    /// </summary>
    internal volatile bool IsReady;

    /// <summary>
    /// The active port-forwarder, retained so it can be disposed when the parent cluster
    /// stops (via <c>ResourceStoppedEvent</c>) or when the AppHost shuts down — whichever
    /// comes first.
    /// </summary>
    internal K3sInProcessPortForwarder? Forwarder { get; set; }

    /// <summary>
    /// The environment variable name used to inject the service URL into dependents
    /// (<c>services__{name}__url</c>), following the Aspire service-discovery convention.
    /// </summary>
    public string? ConnectionStringEnvironmentVariable => $"services__{Name}__url";

    /// <summary>
    /// Gets the manifest expression for the service URL.
    /// Resolves to <c>http(s)://localhost:{hostPort}</c> when the endpoint is ready.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            // ReferenceExpression.Create only accepts IManifestExpressionProvider in
            // format holes, not plain value types. Pre-compute to a string first.
            var url = IsReady && HostPort > 0 ? $"{Scheme}://localhost:{HostPort}" : string.Empty;
            return ReferenceExpression.Create($"{url}");
        }
    }

    /// <summary>
    /// Returns the host-accessible service URL, or <see langword="null"/> if the
    /// port-forward is not yet active.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> until the endpoint transitions to <c>Running</c>.
    /// Declare <c>WaitFor(endpoint)</c> on dependent resources so that this value is
    /// always populated by the time their environment variables are evaluated.
    /// </remarks>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(
            IsReady && HostPort > 0
                ? $"{Scheme}://localhost:{HostPort}"
                : (string?)null);
}
