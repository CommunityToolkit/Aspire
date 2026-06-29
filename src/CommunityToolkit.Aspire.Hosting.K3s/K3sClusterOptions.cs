using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// Advanced options for <see cref="K3sBuilderExtensions.AddK3sCluster"/>.
/// </summary>
/// <remarks>
/// <para>
/// Most settings are exposed directly as fluent builder methods — prefer those for
/// discoverability and TypeScript polyglot compatibility:
/// </para>
/// <list type="bullet">
///   <item><see cref="K3sBuilderExtensions.WithAgentCount"/> — number of worker nodes</item>
///   <item><see cref="K3sBuilderExtensions.WithK3sVersion"/> — k3s image tag</item>
///   <item><see cref="K3sBuilderExtensions.WithPodSubnet"/> — pod CIDR (<c>--cluster-cidr</c>)</item>
///   <item><see cref="K3sBuilderExtensions.WithServiceSubnet"/> — service CIDR (<c>--service-cidr</c>)</item>
///   <item><see cref="K3sBuilderExtensions.WithDisabledComponent"/> — disable built-in components</item>
///   <item><see cref="K3sBuilderExtensions.WithExtraArg"/> — raw k3s server arguments</item>
///   <item><see cref="K3sBuilderExtensions.WithHelmImage"/> — Helm installer image</item>
///   <item><see cref="K3sBuilderExtensions.WithKubectlImage"/> — kubectl image</item>
///   <item><see cref="K3sBuilderExtensions.WithDataVolume"/> — persistent data volume</item>
///   <item><see cref="K3sBuilderExtensions.WithLifetime"/> — container lifetime</item>
/// </list>
/// </remarks>
public sealed class K3sClusterOptions
{
    /// <summary>
    /// Gets or sets the pod IP address range in CIDR notation (<c>--cluster-cidr</c>).
    /// Defaults to k3s built-in value of <c>10.42.0.0/16</c> when <see langword="null"/>.
    /// </summary>
    public string? ClusterCidr { get; set; }

    /// <summary>
    /// Gets or sets the Service cluster IP address range in CIDR notation (<c>--service-cidr</c>).
    /// Defaults to k3s built-in value of <c>10.43.0.0/16</c> when <see langword="null"/>.
    /// </summary>
    public string? ServiceCidr { get; set; }

    /// <summary>
    /// Gets the list of built-in k3s components to disable (each passed as <c>--disable=&lt;component&gt;</c>).
    /// </summary>
    /// <remarks>
    /// Common values: <c>traefik</c>, <c>coredns</c>, <c>local-storage</c>.
    /// Note that <c>servicelb</c> and <c>metrics-server</c> are already disabled by default.
    /// </remarks>
    public IList<string> DisabledComponents { get; } = new List<string>();

    /// <summary>
    /// Gets the list of raw arguments appended verbatim to the <c>k3s server</c> command.
    /// </summary>
    public IList<string> ExtraArgs { get; } = new List<string>();

    /// <summary>
    /// Gets or sets the number of agent (worker) nodes. Defaults to <c>0</c> (single-node cluster).
    /// </summary>
    public int AgentCount { get; set; }

    /// <summary>
    /// Gets or sets the k3s container image tag, e.g. <c>v1.32.3-k3s1</c>.
    /// When <see langword="null"/> the version bundled with this package is used.
    /// </summary>
    public string? ImageTag { get; set; }

    // ── Helm installer image ──────────────────────────────────────────────────

    /// <summary>Gets or sets the container registry for the Helm installer image. Defaults to <c>docker.io</c>.</summary>
    public string HelmRegistry { get; set; } = HelmContainerImageTags.Registry;

    /// <summary>Gets or sets the Helm installer image name. Defaults to <c>alpine/helm</c>.</summary>
    public string HelmImage { get; set; } = HelmContainerImageTags.Image;

    /// <summary>Gets or sets the Helm installer image tag. Defaults to <c>3.17.3</c>.</summary>
    public string HelmTag { get; set; } = HelmContainerImageTags.Tag;

    // ── kubectl image ─────────────────────────────────────────────────────────

    /// <summary>Gets or sets the container registry for the kubectl image. Defaults to <c>docker.io</c>.</summary>
    public string KubectlRegistry { get; set; } = KubectlContainerImageTags.Registry;

    /// <summary>Gets or sets the kubectl image name. Defaults to <c>alpine/kubectl</c>.</summary>
    public string KubectlImage { get; set; } = KubectlContainerImageTags.Image;

    /// <summary>Gets or sets the kubectl image tag. Defaults to <c>1.36.0</c>.</summary>
    public string KubectlTag { get; set; } = KubectlContainerImageTags.Tag;
}
