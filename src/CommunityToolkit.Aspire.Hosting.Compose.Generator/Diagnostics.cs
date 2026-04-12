using Microsoft.CodeAnalysis;

namespace CommunityToolkit.Aspire.Hosting.Compose.Generator;

/// <summary>
/// Diagnostic descriptors for the compose source generator.
/// </summary>
internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor ComposeFileNotFound = new(
        id: "COMPOSE001",
        title: "Compose file not found",
        messageFormat: "Compose file '{0}' not found. The generated class will have no service constants.",
        category: "CommunityToolkit.Aspire.Hosting.Compose",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
