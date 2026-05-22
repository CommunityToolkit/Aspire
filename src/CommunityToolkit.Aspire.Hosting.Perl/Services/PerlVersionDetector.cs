namespace CommunityToolkit.Aspire.Hosting.Perl.Services;

/// <summary>
/// Detects the Perl version through multiple strategies: file-based conventions,
/// CLI execution, or perlbrew environment metadata.
/// </summary>
internal static class PerlVersionDetector
{
    /// <summary>
    /// Detects the Perl version from a <c>.perl-version</c> file in the specified directory.
    /// This follows the perlbrew convention for pinning a project to a specific Perl version.
    /// </summary>
    /// <param name="appDirectory">The resolved application directory to search in.</param>
    /// <returns>The version string (e.g., <c>5.38.0</c>) or <c>null</c> if the file doesn't exist.</returns>
    public static string? DetectVersionFromFile(string appDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(appDirectory);

        var versionFile = Path.Combine(appDirectory, ".perl-version");
        if (!File.Exists(versionFile))
        {
            return null;
        }

        var content = File.ReadAllText(versionFile).Trim();
        return string.IsNullOrEmpty(content) ? null : NormalizeVersionString(content);
    }

    /// <summary>
    /// Extracts the Perl version from a <see cref="PerlbrewEnvironment"/>.
    /// </summary>
    /// <param name="environment">The perlbrew environment.</param>
    /// <returns>The version string (e.g., <c>5.38.0</c>).</returns>
    public static string DetectVersionFromPerlbrew(PerlbrewEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        // PerlbrewEnvironment.Version is like "perl-5.38.0"
        return NormalizeVersionString(environment.Version);
    }

    /// <summary>
    /// Strips the <c>v</c> prefix and <c>perl-</c> prefix from version strings to produce
    /// a clean numeric version like <c>5.38.0</c>.
    /// </summary>
    /// <param name="version">The version string to normalize.</param>
    /// <returns>The normalized numeric version string.</returns>
    internal static string NormalizeVersionString(string version)
    {
        var normalized = version.Trim();

        // Strip "perl-" prefix (perlbrew convention)
        if (normalized.StartsWith("perl-", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["perl-".Length..];
        }

        // Strip "v" prefix (perl $^V output convention)
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        return normalized;
    }
}
