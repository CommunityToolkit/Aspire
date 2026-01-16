using Aspire.Hosting.JavaScript;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a JavaScript application running from a yarn workspace package.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory of the workspace.</param>
/// <param name="workspaceName">The yarn workspace package name to run. (used in yarn workspace &lt;workspaceName&gt; run dev)</param>
/// <param name="command">The command to run (default is 'yarn').</param>
public class YarnWorkspaceAppResource(string name, string workingDirectory, string workspaceName, string command = "yarn")
    : JavaScriptAppResource(name, command, workingDirectory)
{
    /// <summary>
    /// Gets the yarn workspace package name.
    /// </summary>
    public string WorkspaceName { get; } = workspaceName;
}
