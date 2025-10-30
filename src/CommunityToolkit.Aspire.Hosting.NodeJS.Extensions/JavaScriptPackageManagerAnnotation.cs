using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Tracks which JavaScript package manager was configured for a monorepo workspace (via WithNpm/WithYarn/WithPnpm).
/// </summary>
/// <param name="packageManager">The name of the JavaScript package manager (e.g., "npm", "yarn", "pnpm").</param>
public sealed class JavaScriptPackageManagerConfiguredAnnotation(string packageManager) : IResourceAnnotation
{
    /// <summary>
    /// Gets the name of the JavaScript package manager that was configured.
    /// </summary>
    public string PackageManager { get; } = packageManager;
}