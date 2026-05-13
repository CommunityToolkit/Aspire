namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a k3s agent (worker) node that is a child of a <see cref="K3sClusterResource"/>.
/// <para>
/// Agent nodes run <c>k3s agent</c> and join the cluster by connecting to the server's API
/// server at <c>https://{serverName}:6443</c>, resolved via DCP's built-in Docker DNS.
/// Agents start immediately alongside the server (no <c>WaitFor</c> dependency) and use
/// k3s's built-in retry loop to connect once the server becomes reachable. The cluster's
/// health check waits for all <c>1 + <see cref="K3sClusterResource.AgentCount"/></c> nodes
/// to reach <c>Ready</c> state before transitioning to <c>Running</c>.
/// </para>
/// </summary>
/// <param name="name">The resource name (e.g. <c>k8s-agent-0</c>).</param>
/// <param name="cluster">The parent k3s cluster resource.</param>
public sealed class K3sAgentResource(string name, K3sClusterResource cluster)
    : ContainerResource(name), IResourceWithParent<K3sClusterResource>
{
    /// <inheritdoc />
    public K3sClusterResource Parent { get; } = cluster
        ?? throw new ArgumentNullException(nameof(cluster));
}
