using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Node.js application running under Nx.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory of the application.</param>
/// <param name="appName">The Nx app name to run (used in 'nx serve {appName}').</param>
public class NxAppResource(string name, string workingDirectory, string appName)
    : ExecutableResource(name, "nx", workingDirectory)
{
    /// <summary>
    /// Gets the Nx application name.
    /// </summary>
    public string AppName { get; } = appName;
}