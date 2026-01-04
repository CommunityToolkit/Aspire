using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a pnpm workspace.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory of the pnpm workspace.</param>
public class PnpmWorkspaceResource(string name, string workingDirectory) : Resource(name)
{
    /// <summary>
    /// Gets the working directory of the pnpm workspace.
    /// </summary>
    public string WorkingDirectory { get; } = workingDirectory;
}
