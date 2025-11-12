using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an Nx monorepo workspace.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory of the Nx workspace.</param>
public class NxResource(string name, string workingDirectory) : Resource(name)
{
    /// <summary>
    /// Gets the working directory of the Nx workspace.
    /// </summary>
    public string WorkingDirectory { get; } = workingDirectory;
}