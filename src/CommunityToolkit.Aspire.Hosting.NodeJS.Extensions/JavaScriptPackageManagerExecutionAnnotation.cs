using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Indicates that apps in this monorepo workspace should be executed via a package manager wrapper (e.g., npx, yarn, pnpm).
/// </summary>
/// <param name="packageManager">The name of the JavaScript package manager (e.g., "npm", "yarn", "pnpm").</param>
public sealed class JavaScriptPackageManagerExecutionAnnotation(string packageManager) : IResourceAnnotation
{
    /// <summary>
    /// Gets the name of the JavaScript package manager to use for executing apps.
    /// </summary>
    public string PackageManager { get; } = packageManager;
}
