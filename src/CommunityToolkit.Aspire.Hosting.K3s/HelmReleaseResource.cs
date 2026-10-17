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
    /// Values resolved when the container starts — parameters, expressions, endpoints — keyed by
    /// Helm value path, each naming the environment variable that carries it into the container.
    /// Carried in the environment rather than written into the install script so a secret never
    /// lands in a file. Populated by <c>WithHelmValue</c>; a key is in this or
    /// <see cref="HelmValues"/>, never both.
    /// </summary>
    internal Dictionary<string, string> HelmValueVariables { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Variables handed out so far. Never decremented, so a key moved back to a literal value
    /// cannot leave a later key reusing its variable.
    /// </summary>
    internal int HelmValueVariableCount { get; set; }

    /// <summary>
    /// The absolute host path of a chart directory, when the chart is local rather than in a
    /// repository or an OCI registry. Bind-mounted read-only into the container.
    /// </summary>
    internal string? LocalChartDirectory { get; set; }

    /// <summary>
    /// Absolute host paths of values files to inject into the helm container via
    /// <c>--values /helm-values/{filename}</c>.
    /// Populated by <c>WithHelmValuesFile</c>.
    /// </summary>
    internal List<string> ValuesFiles { get; } = [];
}
