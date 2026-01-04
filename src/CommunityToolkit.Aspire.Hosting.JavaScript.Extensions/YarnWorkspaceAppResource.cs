using Aspire.Hosting.JavaScript;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Node.js application running from a yarn workspace package.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory of the workspace.</param>
/// <param name="workspaceName">The yarn workspace package name to run.</param>
/// <param name="script">The package script to run.</param>
/// <param name="command">The command to run (default is 'yarn').</param>
public class YarnWorkspaceAppResource(string name, string workingDirectory, string workspaceName, string script, string command = "yarn")
    : JavaScriptAppResource(name, command, workingDirectory)
{
    /// <summary>
    /// Gets the yarn workspace package name.
    /// </summary>
    public string WorkspaceName { get; } = workspaceName;

    /// <summary>
    /// Gets the script to run from the workspace package.
    /// </summary>
    public string Script { get; } = script;
}
