using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Deno;

/// <summary>
/// A resource that represents a Deno application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="command">The command to execute.</param>
/// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
public class DenoAppResource(string name, string command, string workingDirectory)
    : ExecutableResource(ThrowIfNull(name), ThrowIfNull(command), ThrowIfNull(workingDirectory)), IResourceWithServiceDiscovery
{
    private static string ThrowIfNull([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        => argument ?? throw new ArgumentNullException(paramName);
}
