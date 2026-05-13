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
}
