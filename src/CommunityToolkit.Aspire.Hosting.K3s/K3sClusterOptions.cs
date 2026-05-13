namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// Configuration options for a k3s cluster resource.
/// </summary>
public sealed class K3sClusterOptions
{
    /// <summary>
    /// Gets or sets the CIDR range for pod IPs (passed as <c>--cluster-cidr</c>).
    /// </summary>
    public string? ClusterCidr { get; set; }

    /// <summary>
    /// Gets or sets the CIDR range for service IPs (passed as <c>--service-cidr</c>).
    /// </summary>
    public string? ServiceCidr { get; set; }

    /// <summary>
    /// Gets the list of k3s components to disable (each passed as <c>--disable=&lt;component&gt;</c>).
    /// </summary>
    public IList<string> DisabledComponents { get; } = new List<string>();

    /// <summary>
    /// Gets the list of raw extra arguments appended to the <c>k3s server</c> command.
    /// </summary>
    public IList<string> ExtraArgs { get; } = new List<string>();

    /// <summary>
    /// Gets or sets the number of agent (worker) nodes to add to the cluster.
    /// Equivalent to k3d's <c>--agents N</c> flag.
    /// Defaults to <c>0</c> (single-node cluster).
    /// </summary>
    public int AgentCount { get; set; }

    /// <summary>
    /// Gets or sets the k3s image tag (e.g. <c>v1.31.4-k3s1</c>).
    /// When <see langword="null"/>, the default tag embedded in the package is used.
    /// </summary>
    public string? ImageTag { get; set; }

    // ── Helm installer image ──────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the registry for the Helm installer container image.
    /// Defaults to <c>docker.io</c>.
    /// </summary>
    public string HelmRegistry { get; set; } = HelmContainerImageTags.Registry;

    /// <summary>
    /// Gets or sets the Helm installer container image name.
    /// Defaults to <c>alpine/helm</c>.
    /// </summary>
    public string HelmImage { get; set; } = HelmContainerImageTags.Image;

    /// <summary>
    /// Gets or sets the Helm installer container image tag.
    /// Defaults to <c>3.17.3</c>.
    /// </summary>
    public string HelmTag { get; set; } = HelmContainerImageTags.Tag;

    // ── kubectl image ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the registry for the kubectl container image used by manifest applies.
    /// Defaults to <c>docker.io</c>.
    /// </summary>
    public string KubectlRegistry { get; set; } = KubectlContainerImageTags.Registry;

    /// <summary>
    /// Gets or sets the kubectl container image name used by manifest applies.
    /// Defaults to <c>alpine/k8s</c>.
    /// </summary>
    public string KubectlImage { get; set; } = KubectlContainerImageTags.Image;

    /// <summary>
    /// Gets or sets the kubectl container image tag used by manifest applies.
    /// Defaults to <c>1.32.3</c>.
    /// </summary>
    public string KubectlTag { get; set; } = KubectlContainerImageTags.Tag;
}
