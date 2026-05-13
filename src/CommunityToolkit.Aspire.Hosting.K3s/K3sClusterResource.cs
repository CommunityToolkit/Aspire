using k8s.KubeConfigModels;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a k3s Kubernetes cluster running as a privileged container resource.
/// </summary>
/// <param name="name">The resource name.</param>
public sealed class K3sClusterResource(string name) : ContainerResource(name)
{
    internal const string ApiServerEndpointName = "api";

    /// <summary>
    /// Gets the admin kubeconfig for host-side processes
    /// (<c>server: https://localhost:{allocatedPort}</c>).
    /// Populated by <c>K3sReadinessHealthCheck</c> after the cluster passes <c>/healthz</c>.
    /// Serialise with <c>KubernetesYaml.Serialize(AdminKubeconfig)</c> when needed.
    /// </summary>
    internal K8SConfiguration? AdminKubeconfig { get; set; }

    /// <summary>
    /// Gets the kubeconfig for containers on the DCP Docker network
    /// (<c>server: https://{resourceName}:6443</c>).
    /// Populated by <c>K3sReadinessHealthCheck</c> after the cluster passes <c>/healthz</c>.
    /// </summary>
    internal K8SConfiguration? ContainerKubeconfig { get; set; }

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
    public IReadOnlyDictionary<string, string> HelmReleases => _helmReleases;

    internal void AddHelmRelease(string resourceName, string releaseName) =>
        _helmReleases.TryAdd(resourceName, releaseName);

    private readonly List<string> _manifests = [];

    /// <summary>Names of registered <see cref="K8sManifestResource"/> children.</summary>
    public IReadOnlyList<string> Manifests => _manifests;

    internal void AddManifest(string resourceName) => _manifests.Add(resourceName);
}
