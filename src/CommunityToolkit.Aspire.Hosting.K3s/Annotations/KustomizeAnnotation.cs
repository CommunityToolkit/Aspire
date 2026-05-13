namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Annotation that describes a Kustomize overlay to apply into the cluster.
/// </summary>
/// <param name="path">Path to the kustomization directory or remote URL.</param>
public sealed class KustomizeAnnotation(string path) : IResourceAnnotation
{
    /// <summary>Gets the kustomization path.</summary>
    public string Path { get; } = path;
}
