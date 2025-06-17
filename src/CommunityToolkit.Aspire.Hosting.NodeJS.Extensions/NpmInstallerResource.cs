using System.Runtime.InteropServices;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an npm package installer.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
/// <param name="useCI">Whether to use 'npm ci' instead of 'npm install'.</param>
public class NpmInstallerResource(string name, string workingDirectory, bool useCI = false)
    : ExecutableResource(name, GetCommand(), workingDirectory)
{
    /// <summary>
    /// Gets whether to use 'npm ci' instead of 'npm install'.
    /// </summary>
    public bool UseCI { get; } = useCI;

    /// <summary>
    /// Gets the install command to use.
    /// </summary>
    public string InstallCommand => UseCI ? "ci" : "install";

    /// <summary>
    /// Gets the expected lockfile name.
    /// </summary>
    public string LockfileName => "package-lock.json";

    private static string GetCommand()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "npm";
    }

    /// <summary>
    /// Gets the arguments for the npm install command.
    /// </summary>
    /// <returns>The arguments array.</returns>
    public string[] GetArguments()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ["/c", "npm", InstallCommand];
        }
        return [InstallCommand];
    }
}