namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents one or more Kubernetes YAML manifests applied to the parent k3s cluster via
/// Server-Side Apply. This is a child resource of <see cref="K3sClusterResource"/>, following
/// the same parent-child pattern as <see cref="HelmReleaseResource"/>.
/// <para>
/// No <c>kubectl</c> binary is required — the KubernetesClient library handles the apply.
/// CRDs reach the <c>Established</c> condition before the resource transitions to
/// <c>Running</c>, so dependent resources can safely <c>WaitFor</c> the manifest.
/// </para>
/// </summary>
/// <param name="name">The Aspire resource name.</param>
/// <param name="path">
/// Path to a single YAML file, a directory, or a glob pattern (<c>*.yaml</c>).
/// Directories and globs are expanded lexicographically.
/// </param>
/// <param name="cluster">The parent k3s cluster resource.</param>
public sealed class K8sManifestResource(string name, string path, K3sClusterResource cluster)
    : Resource(name), IResourceWithParent<K3sClusterResource>, IResourceWithWaitSupport
{
    /// <inheritdoc />
    public K3sClusterResource Parent { get; } = cluster ?? throw new ArgumentNullException(nameof(cluster));

    /// <summary>Gets the manifest path, directory, or glob.</summary>
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    /// <summary>
    /// Set to <see langword="true"/> by the lifecycle after all objects are applied and
    /// (for CRDs) the <c>Established</c> condition is confirmed.
    /// </summary>
    internal volatile bool IsReady;

    /// <summary>Services to expose via in-process port-forward after the manifest is applied.</summary>
    internal List<ManifestEndpointDefinition> EndpointDefinitions { get; } = [];
}

/// <summary>Describes a service endpoint to expose from a <see cref="K8sManifestResource"/>.</summary>
/// <param name="ServiceName">Kubernetes service name.</param>
/// <param name="ServicePort">Service port number.</param>
/// <param name="EndpointName">Friendly name shown in the dashboard.</param>
/// <param name="Namespace">Kubernetes namespace where the service lives.</param>
internal sealed record ManifestEndpointDefinition(
    string ServiceName,
    int ServicePort,
    string EndpointName,
    string Namespace);
