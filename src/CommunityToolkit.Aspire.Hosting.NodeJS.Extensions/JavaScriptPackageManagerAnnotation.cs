using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Represents an annotation for a JavaScript package manager resource within the Aspire hosting environment.
/// </summary>
/// <param name="packageManager">The name of the JavaScript package manager (e.g., "npm", "yarn", "pnpm").</param>
public sealed class JavaScriptPackageManagerAnnotation(string packageManager) : IResourceAnnotation
{
    /// <summary>
    /// Gets the name of the JavaScript package manager.
    /// </summary>
    public string PackageManager { get; } = packageManager;
}