using Aspire.Hosting.JavaScript;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Node.js application running under Vite+.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory of the Vite+ workspace.</param>
/// <param name="directory">The directory of the app within the workspace, relative to the workspace root. It is passed to <c>vp -C</c>.</param>
/// <param name="command">The command to run (default is 'vp').</param>
public class VitePlusAppResource(string name, string workingDirectory, string directory, string command = VitePlusHostingExtensions.CliName)
    : JavaScriptAppResource(name, command, workingDirectory)
{
    /// <summary>
    /// Gets the directory of the app within the Vite+ workspace, relative to the workspace root.
    /// </summary>
    public string Directory { get; } = directory;
}
