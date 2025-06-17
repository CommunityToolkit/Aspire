using System.Runtime.InteropServices;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a pnpm package installer.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
public class PnpmInstallerResource(string name, string workingDirectory)
    : ExecutableResource(name, GetCommand(), workingDirectory)
{
    /// <summary>
    /// Gets the install command to use.
    /// </summary>
    public string InstallCommand => "install";

    /// <summary>
    /// Gets the expected lockfile name.
    /// </summary>
    public string LockfileName => "pnpm-lock.yaml";

    private static string GetCommand()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "pnpm";
    }

    /// <summary>
    /// Gets the arguments for the pnpm install command.
    /// </summary>
    /// <returns>The arguments array.</returns>
    public string[] GetArguments()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ["/c", "pnpm", InstallCommand];
        }
        return [InstallCommand];
    }
}