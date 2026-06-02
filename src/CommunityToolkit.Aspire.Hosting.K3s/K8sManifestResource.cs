#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Kubernetes manifest (or Kustomize overlay) applied to the parent k3s cluster.
/// </summary>
/// <param name="name">The Aspire resource name.</param>
/// <param name="path">Absolute path to the YAML file, plain directory, or Kustomize directory.</param>
/// <param name="cluster">The parent k3s cluster resource.</param>
/// <remarks>
/// The resource runs as an <c>alpine/kubectl</c> container. It polls until the cluster
/// kubeconfig is available, then applies the manifests with <c>kubectl apply --server-side</c>
/// and waits for any CRDs to reach the <c>Established</c> condition before exiting with code 0.
/// Use <c>WaitForCompletion(manifest)</c> on resources that depend on these manifests being applied.
/// </remarks>
[AspireExport(ExposeProperties = true)]
public sealed class K8sManifestResource(string name, string path, K3sClusterResource cluster)
    : ContainerResource(name), IResourceWithParent<K3sClusterResource>
{
    /// <inheritdoc />
    public K3sClusterResource Parent { get; } = cluster ?? throw new ArgumentNullException(nameof(cluster));

    /// <summary>
    /// Gets the absolute host path to the YAML file, plain directory, or Kustomize directory
    /// that contains the manifests to apply.
    /// </summary>
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
}
