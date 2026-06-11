#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Helm chart release installed into a k3s cluster.
/// </summary>
/// <param name="name">The Aspire resource name; also used as the Helm release name.</param>
/// <param name="releaseName">
/// The Helm release name passed to <c>helm upgrade --install</c>.
/// </param>
/// <param name="namespace">The Kubernetes namespace to install the chart into.</param>
/// <param name="cluster">The parent k3s cluster resource.</param>
/// <remarks>
/// The release runs as an <c>alpine/helm</c> container. The container polls until the cluster
/// kubeconfig is available, executes <c>helm upgrade --install --wait</c>, then exits with
/// code 0. Use <c>WaitForCompletion(helmRelease)</c> on resources that depend on the chart
/// being fully installed.
/// </remarks>
[AspireExport(ExposeProperties = true)]
public sealed class HelmReleaseResource(
    string name,
    string releaseName,
    string @namespace,
    K3sClusterResource cluster)
    : ContainerResource(name), IResourceWithParent<K3sClusterResource>
{
    /// <inheritdoc />
    public K3sClusterResource Parent { get; } = cluster ?? throw new ArgumentNullException(nameof(cluster));

    /// <summary>
    /// Gets the Helm release name passed to <c>helm upgrade --install</c>.
    /// </summary>
    public string ReleaseName { get; } = releaseName ?? throw new ArgumentNullException(nameof(releaseName));

    /// <summary>Gets the Kubernetes namespace the chart is installed into.</summary>
    public string Namespace { get; } = @namespace ?? throw new ArgumentNullException(nameof(@namespace));

    internal string? Chart { get; set; }
    internal string? RepoUrl { get; set; }
    internal string? Version { get; set; }
    internal Dictionary<string, string> HelmValues { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Absolute host paths of values files to inject into the helm container via
    /// <c>--values /helm-values/{filename}</c>.
    /// Populated by <c>WithHelmValuesFile</c>.
    /// </summary>
    internal List<string> ValuesFiles { get; } = [];
}
