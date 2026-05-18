#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a k3s Kubernetes cluster running as a privileged container resource.
/// </summary>
/// <param name="name">The resource name.</param>
[AspireExport(ExposeProperties = true)]
public sealed class K3sClusterResource(string name) : ContainerResource(name)
{
    internal const string ApiServerEndpointName = "api";

    /// <summary>Container image settings for the Helm installer, resolved from cluster options.</summary>
    internal (string Registry, string Image, string Tag) HelmImageInfo { get; set; }
        = ("docker.io", "alpine/helm", "3.17.3");

    /// <summary>Container image settings for the kubectl manifest applier, resolved from cluster options.</summary>
    internal (string Registry, string Image, string Tag) KubectlImageInfo { get; set; }
        = ("docker.io", "rancher/kubectl", "v1.32.3");

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
