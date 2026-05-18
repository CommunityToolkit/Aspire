#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents one or more Kubernetes YAML manifests applied to the parent k3s cluster via
/// <c>kubectl apply --server-side</c> running inside a <c>rancher/kubectl</c> container.
/// <para>
/// The container polls for the cluster kubeconfig (written when the cluster health check
/// first passes), applies the manifests, waits for any CRDs to reach <c>Established</c>,
/// then exits with code 0. Use <c>WaitForCompletion(manifest)</c> on dependent resources.
/// </para>
/// </summary>
/// <param name="name">The Aspire resource name.</param>
/// <param name="path">Absolute path to a single YAML file or a directory.</param>
/// <param name="cluster">The parent k3s cluster resource.</param>
[AspireExport(ExposeProperties = true)]
public sealed class K8sManifestResource(string name, string path, K3sClusterResource cluster)
    : ContainerResource(name), IResourceWithParent<K3sClusterResource>
{
    /// <inheritdoc />
    public K3sClusterResource Parent { get; } = cluster ?? throw new ArgumentNullException(nameof(cluster));

    /// <summary>Gets the manifest path or directory on the host.</summary>
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
}
