namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Helm chart release deployed to a k3s cluster.
/// <para>
/// Runs as an <c>alpine/helm</c> container on the DCP network. The container polls for the
/// cluster kubeconfig (written when the cluster health check first passes), executes
/// <c>helm upgrade --install --wait</c>, and exits with code 0 on success. Use
/// <c>WaitForCompletion(helmRelease)</c> on resources that depend on the release being installed.
/// </para>
/// </summary>
/// <param name="name">The Aspire resource name (also used as the Helm release name).</param>
/// <param name="releaseName">The Helm release name passed to <c>helm upgrade --install</c>.</param>
/// <param name="namespace">The Kubernetes namespace to install into.</param>
/// <param name="cluster">The parent k3s cluster resource.</param>
public sealed class HelmReleaseResource(
    string name,
    string releaseName,
    string @namespace,
    K3sClusterResource cluster)
    : ContainerResource(name), IResourceWithParent<K3sClusterResource>
{
    /// <inheritdoc />
    public K3sClusterResource Parent { get; } = cluster ?? throw new ArgumentNullException(nameof(cluster));

    /// <summary>Gets the Helm release name.</summary>
    public string ReleaseName { get; } = releaseName ?? throw new ArgumentNullException(nameof(releaseName));

    /// <summary>Gets the target Kubernetes namespace.</summary>
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
