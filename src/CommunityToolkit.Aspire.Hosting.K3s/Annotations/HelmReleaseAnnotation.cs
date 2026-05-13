namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Annotation that describes a Helm chart release to install into the cluster.
/// </summary>
/// <param name="releaseName">The Helm release name.</param>
/// <param name="chart">The chart name or local path.</param>
/// <param name="namespace">The Kubernetes namespace to install into.</param>
/// <param name="repoUrl">Optional Helm repository URL containing the chart.</param>
/// <param name="version">Optional chart version.</param>
public sealed class HelmReleaseAnnotation(
    string releaseName,
    string chart,
    string @namespace,
    string? repoUrl,
    string? version) : IResourceAnnotation
{
    /// <summary>Gets the Helm release name.</summary>
    public string ReleaseName { get; } = releaseName;

    /// <summary>Gets the chart name or local path.</summary>
    public string Chart { get; } = chart;

    /// <summary>Gets the target Kubernetes namespace.</summary>
    public string Namespace { get; } = @namespace;

    /// <summary>Gets the optional Helm repository URL.</summary>
    public string? RepoUrl { get; } = repoUrl;

    /// <summary>Gets the optional chart version.</summary>
    public string? Version { get; } = version;

    /// <summary>Gets the extra <c>--set</c> values passed to <c>helm install</c>.</summary>
    public IDictionary<string, string> Values { get; } = new Dictionary<string, string>();
}
