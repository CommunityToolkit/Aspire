namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Annotation that signals the Kubernetes Dashboard should be installed alongside the cluster.
/// </summary>
/// <param name="version">The Kubernetes Dashboard chart version to install.</param>
public sealed class KubernetesDashboardAnnotation(string? version = null) : IResourceAnnotation
{
    /// <summary>Gets the dashboard chart version, or <see langword="null"/> to use the latest.</summary>
    public string? Version { get; } = version;
}
