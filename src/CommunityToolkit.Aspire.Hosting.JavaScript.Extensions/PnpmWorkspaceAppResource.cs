using Aspire.Hosting.JavaScript;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a JavaScript application running from a pnpm workspace package.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory of the workspace.</param>
/// <param name="filter">The pnpm filter to use when running the package. (used in pnpm --filter &lt;filter&gt; run dev)</param>
/// <param name="command">The command to run (default is 'pnpm').</param>
public class PnpmWorkspaceAppResource(string name, string workingDirectory, string filter, string command = "pnpm")
    : JavaScriptAppResource(name, command, workingDirectory)
{
    /// <summary>
    /// Gets the pnpm filter used for the workspace package.
    /// </summary>
    public string Filter { get; } = filter;
}
