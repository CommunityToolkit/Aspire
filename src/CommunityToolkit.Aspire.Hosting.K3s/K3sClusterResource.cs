#pragma warning disable ASPIREATS001 // AspireExport is experimental

using CommunityToolkit.Aspire.Hosting;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a k3s Kubernetes cluster running as a privileged container resource.
/// Injects the cluster's kubeconfig into a dependent resource.
/// <list type="bullet">
///   <item>Host processes receive <c>KUBECONFIG=&lt;host-path&gt;/local/kubeconfig.yaml</c>.</item>
///   <item>Containers receive <c>KUBECONFIG=/tmp/k3s-kubeconfig.yaml</c> and a file-level
///     bind-mount of the container-network kubeconfig variant. Both are applied by the
///     <c>BeforeStartEvent</c> subscriber registered in <c>AddK3sCluster</c>.
///   </item>
/// </list>
/// </summary>
/// <param name="name">The resource name.</param>
[AspireExport(ExposeProperties = true)]
public sealed class K3sClusterResource(string name)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string ApiServerEndpointName = "api";

    /// <summary>Container image settings for the Helm installer, resolved from cluster options.</summary>
    internal (string Registry, string Image, string Tag) HelmImageInfo { get; set; }
        = (HelmContainerImageTags.Registry, HelmContainerImageTags.Image, HelmContainerImageTags.Tag);

    /// <summary>Container image settings for the kubectl manifest applier, resolved from cluster options.</summary>
    internal (string Registry, string Image, string Tag) KubectlImageInfo { get; set; }
        = (KubectlContainerImageTags.Registry, KubectlContainerImageTags.Image, KubectlContainerImageTags.Tag);

    /// <summary>
    /// Host-side directory that holds all kubeconfig variants for this cluster.
    /// Set by <c>AddK3sCluster</c> to <c>AppHostDirectory/.k3s/{name}/</c>.
    /// Sub-directories:
    /// <list type="bullet">
    ///   <item><c>cluster/kubeconfig.yaml</c> — raw file written by k3s (bind-mounted)</item>
    ///   <item><c>local/kubeconfig.yaml</c> — <c>server: https://localhost:{port}</c> (host processes)</item>
    ///   <item><c>container/kubeconfig.yaml</c> — <c>server: https://{name}:6443</c> (DCP-network containers)</item>
    /// </list>
    /// </summary>
    internal string? KubeconfigDirectory { get; set; }

    // ── IResourceWithConnectionString ─────────────────────────────────────────
    // Exposes the kubeconfig path as the connection string so the standard
    // WithReference(cluster) overload injects KUBECONFIG for host processes.
    // Containers get a bind-mount + container-network path via BeforeStartEvent.

    /// <summary>Overrides the default <c>ConnectionStrings__</c> prefix so Aspire injects <c>KUBECONFIG</c>.</summary>
    public string? ConnectionStringEnvironmentVariable => "KUBECONFIG";

    /// <summary>Manifest expression for the local kubeconfig path.</summary>
    public ReferenceExpression ConnectionStringExpression =>
        KubeconfigDirectory is null
            ? ReferenceExpression.Create($"")
            : ReferenceExpression.Create($"{KubeconfigDirectory}/local/kubeconfig.yaml");

    /// <summary>Returns the host-accessible kubeconfig path for this cluster.</summary>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(
            KubeconfigDirectory is null
                ? null
                : Path.Combine(KubeconfigDirectory, "local", "kubeconfig.yaml"));

    private EndpointReference? _apiEndpoint;

    /// <summary>Gets the endpoint reference for the k3s API server (port 6443).</summary>
    public EndpointReference ApiEndpoint => _apiEndpoint ??= new(this, ApiServerEndpointName);

    // ── Child resource tracking (Postgres pattern) ────────────────────────────

    /// <summary>
    /// Number of agent (worker) nodes. The health check waits for all
    /// <c>1 + AgentCount</c> nodes to reach <c>Ready</c> state before marking the cluster healthy.
    /// </summary>
    internal int AgentCount { get; set; }

    private readonly List<K3sAgentResource> _agentResources = [];

    /// <summary>Agent resource instances, used to propagate annotations (e.g. lifetime) to all nodes.</summary>
    internal IReadOnlyList<K3sAgentResource> AgentResources => _agentResources;

    internal void AddAgentResource(K3sAgentResource agent) => _agentResources.Add(agent);

    private readonly Dictionary<string, string> _helmReleases =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A dictionary of registered Helm releases keyed by resource name.</summary>
    [AspireExportIgnore(Reason = "Internal tracking collection; not needed by guest SDK consumers.")]
    public IReadOnlyDictionary<string, string> HelmReleases => _helmReleases;

    internal void AddHelmRelease(string resourceName, string releaseName) =>
        _helmReleases.TryAdd(resourceName, releaseName);

    private readonly List<string> _manifests = [];

    /// <summary>Names of registered <see cref="K8sManifestResource"/> children.</summary>
    [AspireExportIgnore(Reason = "Internal tracking collection; not needed by guest SDK consumers.")]
    public IReadOnlyList<string> Manifests => _manifests;

    internal void AddManifest(string resourceName) => _manifests.Add(resourceName);
}
