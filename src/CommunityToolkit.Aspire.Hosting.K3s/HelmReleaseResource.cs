namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Helm chart release deployed to a k3s cluster.
/// Appears as a distinct dashboard entry; transitions <c>Starting → Running</c>
/// when all pods reach the <c>Ready</c> state.
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
    : Resource(name), IResourceWithParent<K3sClusterResource>, IResourceWithWaitSupport
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
    internal List<HelmEndpointDefinition> EndpointDefinitions { get; } = [];

    /// <summary>
    /// Set to <see langword="true"/> by the lifecycle when the helm install completes and
    /// all pods are ready. The <c>WaitFor(helmRelease)</c> health check polls this flag.
    /// </summary>
    internal volatile bool IsReady;
}

/// <summary>Describes a Kubernetes service endpoint to expose from a Helm release.</summary>
/// <param name="ServiceName">The Kubernetes service name.</param>
/// <param name="ServicePort">The service port number.</param>
/// <param name="EndpointName">A friendly name shown in the dashboard.</param>
internal sealed record HelmEndpointDefinition(string ServiceName, int ServicePort, string EndpointName);
