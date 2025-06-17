using System.Runtime.InteropServices;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a yarn package installer.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
public class YarnInstallerResource(string name, string workingDirectory)
    : ExecutableResource(name, GetCommand(), workingDirectory)
{
    /// <summary>
    /// Gets the install command to use.
    /// </summary>
    public string InstallCommand => "install";

    /// <summary>
    /// Gets the expected lockfile name.
    /// </summary>
    public string LockfileName => "yarn.lock";

    private static string GetCommand()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "yarn";
    }

    /// <summary>
    /// Gets the arguments for the yarn install command.
    /// </summary>
    /// <returns>The arguments array.</returns>
    public string[] GetArguments()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ["/c", "yarn", InstallCommand];
        }
        return [InstallCommand];
    }
}