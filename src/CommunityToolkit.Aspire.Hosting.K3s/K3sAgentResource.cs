namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a k3s agent (worker) node belonging to a <see cref="K3sClusterResource"/>.
/// </summary>
/// <param name="name">The resource name, e.g. <c>k8s-agent-0</c>.</param>
/// <param name="cluster">The parent k3s cluster resource.</param>
/// <remarks>
/// Agent nodes run <c>k3s agent</c> and join the server at <c>https://{serverName}:6443</c>
/// using DCP's Docker DNS. They start in parallel with the server without a <c>WaitFor</c>
/// dependency — k3s's built-in retry loop handles the connection timing. The cluster's health
/// check waits for all <c>1 + AgentCount</c> nodes to reach <c>Ready</c> state before the
/// cluster resource transitions to <c>Running</c>. Agent nodes are created automatically by
/// <c>AddK3sCluster</c> when <c>K3sClusterOptions.AgentCount</c> is greater than zero.
/// </remarks>
public sealed class K3sAgentResource(string name, K3sClusterResource cluster)
    : ContainerResource(name), IResourceWithParent<K3sClusterResource>
{
    /// <inheritdoc />
    public K3sClusterResource Parent { get; } = cluster
        ?? throw new ArgumentNullException(nameof(cluster));
}
