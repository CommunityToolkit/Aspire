namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// Controls which kubeconfig server-URL variant is injected via <c>KUBECONFIG_DATA</c>.
/// All variants are delivered as base-64-encoded YAML without writing any file.
/// </summary>
public enum KubeconfigInjectionStrategy
{
    /// <summary>
    /// Selects the server URL automatically based on the resource type:
    /// <list type="bullet">
    ///   <item>Container resources receive the
    ///     container-network URL (<c>https://{resourceName}:6443</c>).</item>
    ///   <item>Projects and executables receive the host URL
    ///     (<c>https://localhost:{allocatedPort}</c>).</item>
    /// </list>
    /// </summary>
    Auto,

    /// <summary>
    /// Always inject the host-network kubeconfig (<c>server: https://localhost:{port}</c>).
    /// Use when a container is launched with <c>--network=host</c> or when the caller
    /// explicitly needs host-side connectivity.
    /// </summary>
    HostNetwork,

    /// <summary>
    /// Always inject the DCP-network kubeconfig
    /// (<c>server: https://{resourceName}:6443</c>).
    /// Use when a host process needs to reach the cluster the same way containers do.
    /// </summary>
    ContainerNetwork,
}
