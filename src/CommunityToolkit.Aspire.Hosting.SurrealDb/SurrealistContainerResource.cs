using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a container resource for Surrealist.
/// </summary>
/// <param name="name">The name of the container resource.</param>
public sealed class SurrealistContainerResource(string name) : ContainerResource(ThrowIfNull(name))
{
    private static string ThrowIfNull([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        => argument ?? throw new ArgumentNullException(paramName);
}