#pragma warning disable ASPIREATS001 // AspireExport is experimental

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// Configuration options passed to <c>AddK3sCluster</c> via its <c>configure</c> callback.
/// </summary>
/// <remarks>
/// All settings are optional. Defaults produce a single-node cluster using the package's
/// bundled k3s, <c>alpine/helm</c>, and <c>alpine/kubectl</c> image versions.
/// </remarks>
[AspireExport(ExposeProperties = true)]
public sealed class K3sClusterOptions
{
    /// <summary>
    /// Gets or sets the pod IP address range in CIDR notation, passed as
    /// <c>--cluster-cidr</c> to the k3s server.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/> (the default), k3s uses its built-in default of
    /// <c>10.42.0.0/16</c>. Can also be set fluently via <c>WithPodSubnet</c>.
    /// </remarks>
    public string? ClusterCidr { get; set; }

    /// <summary>
    /// Gets or sets the Service cluster IP address range in CIDR notation, passed as
    /// <c>--service-cidr</c> to the k3s server.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/> (the default), k3s uses its built-in default of
    /// <c>10.43.0.0/16</c>. Can also be set fluently via <c>WithServiceSubnet</c>.
    /// </remarks>
    public string? ServiceCidr { get; set; }

    /// <summary>
    /// Gets the list of built-in k3s components to disable.
    /// Each entry is passed as <c>--disable=&lt;component&gt;</c> to the k3s server.
    /// </summary>
    /// <remarks>
    /// Common values: <c>traefik</c>, <c>servicelb</c>, <c>metrics-server</c>,
    /// <c>coredns</c>, <c>local-storage</c>. Note that <c>servicelb</c> and
    /// <c>metrics-server</c> are already disabled by default for faster cluster startup.
    /// </remarks>
    public IList<string> DisabledComponents { get; } = new List<string>();

    /// <summary>
    /// Gets the list of raw arguments appended to the <c>k3s server</c> command line.
    /// </summary>
    /// <remarks>
    /// Use this for flags that have no dedicated option in <see cref="K3sClusterOptions"/>.
    /// Prefer the typed properties and the fluent extension methods (<c>WithPodSubnet</c>,
    /// <c>WithDisabledComponent</c>, etc.) when available.
    /// </remarks>
    public IList<string> ExtraArgs { get; } = new List<string>();

    /// <summary>
    /// Gets or sets the number of agent (worker) nodes to add alongside the server.
    /// Defaults to <c>0</c>, which produces a single-node cluster.
    /// </summary>
    /// <remarks>
    /// Each agent runs as a separate container and joins the server automatically.
    /// The cluster health check waits for all <c>1 + AgentCount</c> nodes to be
    /// <c>Ready</c> before the cluster resource transitions to <c>Running</c>.
    /// </remarks>
    public int AgentCount { get; set; }

    /// <summary>
    /// Gets or sets the k3s container image tag, e.g. <c>v1.32.3-k3s1</c>.
    /// When <see langword="null"/> (the default), the version bundled with this package is used.
    /// </summary>
    /// <remarks>
    /// Can also be set fluently after <c>AddK3sCluster</c> via <c>WithK3sVersion</c>.
    /// </remarks>
    public string? ImageTag { get; set; }

    // ── Helm installer image ──────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the container registry for the Helm installer image.
    /// Defaults to <c>docker.io</c>.
    /// </summary>
    public string HelmRegistry { get; set; } = HelmContainerImageTags.Registry;

    /// <summary>
    /// Gets or sets the Helm installer image name. Defaults to <c>alpine/helm</c>.
    /// </summary>
    public string HelmImage { get; set; } = HelmContainerImageTags.Image;

    /// <summary>
    /// Gets or sets the Helm installer image tag. Defaults to <c>3.17.3</c>.
    /// </summary>
    public string HelmTag { get; set; } = HelmContainerImageTags.Tag;

    // ── kubectl image ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the container registry for the kubectl image used by manifest applies.
    /// Defaults to <c>docker.io</c>.
    /// </summary>
    public string KubectlRegistry { get; set; } = KubectlContainerImageTags.Registry;

    /// <summary>
    /// Gets or sets the kubectl image name used by manifest applies.
    /// Defaults to <c>alpine/kubectl</c>.
    /// </summary>
    public string KubectlImage { get; set; } = KubectlContainerImageTags.Image;

    /// <summary>
    /// Gets or sets the kubectl image tag used by manifest applies.
    /// Defaults to <c>1.36.0</c>, aligned with the default k3s server version.
    /// </summary>
    public string KubectlTag { get; set; } = KubectlContainerImageTags.Tag;
}
