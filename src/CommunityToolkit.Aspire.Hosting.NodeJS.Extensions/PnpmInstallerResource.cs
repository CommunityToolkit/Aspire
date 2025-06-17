namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a pnpm package installer.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
public class PnpmInstallerResource(string name, string workingDirectory)
    : ExecutableResource(name, "pnpm", workingDirectory);