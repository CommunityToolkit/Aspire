namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Exposes a Kubernetes service running inside a k3s cluster as an Aspire endpoint resource.
/// <para>
/// An in-process KubernetesClient WebSocket port-forward is started when the cluster is ready.
/// The forwarder binds to <c>0.0.0.0:{hostPort}</c> so both host processes and DCP-network
/// containers can reach the service.
/// </para>
/// <list type="bullet">
///   <item>Host consumers receive <c>services__{name}__url=https://localhost:{port}</c>.</item>
///   <item>Container consumers receive <c>services__{name}__url=https://host.docker.internal:{port}</c>.</item>
/// </list>
/// </summary>
public sealed class K3sServiceEndpointResource(
    string name,
    string serviceName,
    int servicePort,
    string @namespace,
    K3sClusterResource cluster)
    : Resource(name), IResourceWithParent<K3sClusterResource>, IResourceWithWaitSupport
{
    /// <inheritdoc />
    public K3sClusterResource Parent { get; } = cluster ?? throw new ArgumentNullException(nameof(cluster));

    /// <summary>Gets the Kubernetes service name.</summary>
    public string ServiceName { get; } = serviceName ?? throw new ArgumentNullException(nameof(serviceName));

    /// <summary>Gets the service port number.</summary>
    public int ServicePort { get; } = servicePort;

    /// <summary>Gets the Kubernetes namespace containing the service.</summary>
    public string Namespace { get; } = @namespace ?? throw new ArgumentNullException(nameof(@namespace));

    /// <summary>
    /// The host port allocated for the port-forward listener.
    /// Set by <c>RunEndpointAsync</c> before the resource transitions to <c>Running</c>.
    /// Consumers can use this to construct the service URL directly when needed.
    /// </summary>
    public int HostPort { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the port-forward is active and accepting connections.
    /// Set by <c>K3sInProcessPortForwarder</c>; read by the health check.
    /// </summary>
    internal volatile bool IsReady;
}
