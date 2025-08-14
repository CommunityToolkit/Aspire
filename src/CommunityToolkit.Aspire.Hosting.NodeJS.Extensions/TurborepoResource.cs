using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Turborepo monorepo workspace.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory of the Turborepo workspace.</param>
public class TurborepoResource(string name, string workingDirectory) : Resource(name)
{
    /// <summary>
    /// Gets the working directory of the Turborepo workspace.
    /// </summary>
    public string WorkingDirectory { get; } = workingDirectory;
}