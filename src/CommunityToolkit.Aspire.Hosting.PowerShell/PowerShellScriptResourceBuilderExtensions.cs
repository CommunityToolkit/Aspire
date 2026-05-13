#pragma warning disable ASPIREATS001 // AspireExport is experimental

using Aspire.Hosting;
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
    /// <remarks>This overload is not available in polyglot app hosts. Use the string-based overload instead.</remarks>
    [AspireExportIgnore(Reason = "object[] is not ATS-compatible. Use the string-based overload instead.")]
    public static IResourceBuilder<PowerShellScriptResource> WithArgs(
        this IResourceBuilder<PowerShellScriptResource> builder, params object[] args)
    {
        return builder.WithAnnotation(new PowerShellScriptArgsAnnotation(args));
    }

    [AspireExport("withArgs", Description = "Provides string arguments to the PowerShell script")]
    internal static IResourceBuilder<PowerShellScriptResource> WithArgs(
        this IResourceBuilder<PowerShellScriptResource> builder, params string[] args)
    {
        return builder.WithArgs(args.Cast<object>().ToArray());
    }
}

/// <summary>
/// Represents the arguments for a PowerShell script resource.
/// </summary>
/// <param name="Args"></param>
public record PowerShellScriptArgsAnnotation(object[] Args) : IResourceAnnotation;
