using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a yarn workspace.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory of the yarn workspace.</param>
public class YarnWorkspaceResource(string name, string workingDirectory) : Resource(name)
{
    /// <summary>
    /// Gets the working directory of the yarn workspace.
    /// </summary>
    public string WorkingDirectory { get; } = workingDirectory;
}
