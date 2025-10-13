using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.PowerShell;


/// <summary>
/// PowerShell script resource builder extensions.
/// </summary>
public static class PowerShellScriptResourceBuilderExtensions
{
    /// <summary>
    /// Provide arguments to the PowerShell script.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static IResourceBuilder<PowerShellScriptResource> WithArgs(
        this IResourceBuilder<PowerShellScriptResource> builder, params object[] args)
    {
        return builder.WithAnnotation(new PowerShellScriptArgsAnnotation(args));
    }
}

/// <summary>
/// Represents the arguments for a PowerShell script resource.
/// </summary>
/// <param name="Args"></param>
public record PowerShellScriptArgsAnnotation(object[] Args) : IResourceAnnotation;

