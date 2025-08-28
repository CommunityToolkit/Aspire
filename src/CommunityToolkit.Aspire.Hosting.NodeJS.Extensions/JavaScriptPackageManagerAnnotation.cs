using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Represents the annotation for the JavaScript package manager used in a resource.
/// </summary>
/// <param name="packageManager">The name of the JavaScript package manager.</param>
public sealed class JavaScriptPackageManagerAnnotation(string packageManager) : IResourceAnnotation
{
    /// <summary>
    /// Gets the name of the JavaScript package manager.
    /// </summary>
    public string PackageManager { get; } = packageManager;
}