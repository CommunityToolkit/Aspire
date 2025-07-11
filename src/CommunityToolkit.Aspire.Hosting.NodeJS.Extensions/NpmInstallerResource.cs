namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an npm package installer.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
public class NpmInstallerResource(string name, string workingDirectory)
    : ExecutableResource(name, "npm", workingDirectory);