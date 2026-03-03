using System.Text.RegularExpressions;

namespace CommunityToolkit.Aspire.Hosting.Perl.Services;

/// <summary>
/// Parses a Perl cpanfile to extract required module names.
/// Used for preflight checks to determine whether project dependencies
/// are already satisfied before invoking the package manager.
/// </summary>
internal static partial class CpanfileParser
{
    /// <summary>
    /// Extracts module names from <c>requires</c> directives in a cpanfile.
    /// Parses all <c>requires</c> lines regardless of phase (<c>on 'test'</c>, etc.)
    /// and filters out the <c>perl</c> pseudo-module (used for minimum version constraints).
    /// </summary>
    /// <param name="cpanfilePath">The absolute path to the cpanfile.</param>
    /// <returns>A distinct list of required module names.</returns>
    public static IReadOnlyList<string> ParseRequiredModules(string cpanfilePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(cpanfilePath);

        if (!File.Exists(cpanfilePath))
        {
            return [];
        }

        var content = File.ReadAllText(cpanfilePath);
        var matches = RequiresPattern().Matches(content);

        return matches
            .Select(m => m.Groups[1].Value)
            .Where(m => !m.Equals("perl", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
    }

    // Matches: requires 'Module::Name' or requires "Module::Name"
    // With optional version: requires 'Module::Name', '>= 1.0';
    [GeneratedRegex(@"^\s*requires\s+['""]([^'""]+)['""]", RegexOptions.Multiline)]
    private static partial Regex RequiresPattern();
}
