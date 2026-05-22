namespace CommunityToolkit.Aspire.Hosting.Perl;

/// <summary>
/// Handles location of executables within a perlbrew-managed Perl installation.
/// Perlbrew installs Perl versions under <c>$PERLBREW_ROOT/perls/&lt;version&gt;/</c>,
/// with executables in the <c>bin/</c> subdirectory.
/// </summary>
/// <param name="perlbrewRoot">The perlbrew root directory (typically <c>~/perl5/perlbrew</c>).</param>
/// <param name="version">The perlbrew version name (e.g., <c>perl-5.38.0</c>).</param>
internal sealed class PerlbrewEnvironment(string perlbrewRoot, string version)
{
    /// <summary>
    /// The perlbrew root directory.
    /// </summary>
    public string PerlbrewRoot => perlbrewRoot;

    /// <summary>
    /// The perlbrew version name (e.g., <c>perl-5.38.0</c>).
    /// </summary>
    public string Version => version;

    /// <summary>
    /// The full path to the version's installation directory.
    /// </summary>
    public string VersionPath => Path.Combine(perlbrewRoot, "perls", version);

    /// <summary>
    /// The full path to the version's bin directory.
    /// </summary>
    public string BinPath => Path.Combine(perlbrewRoot, "perls", version, "bin");

    /// <summary>
    /// Locates an executable in the perlbrew version's bin directory.
    /// </summary>
    /// <param name="name">The name of the executable (e.g., <c>perl</c>, <c>cpanm</c>).</param>
    /// <returns>The full path to the executable.</returns>
    public string GetExecutable(string name) => Path.Combine(BinPath, name);

    /// <summary>
    /// Resolves the perlbrew root directory from an explicit value, the <c>PERLBREW_ROOT</c>
    /// environment variable, or the default <c>~/perl5/perlbrew</c>.
    /// </summary>
    /// <param name="explicitRoot">An explicit perlbrew root path, or <c>null</c> to auto-detect.</param>
    /// <returns>The resolved perlbrew root directory path.</returns>
    public static string ResolvePerlbrewRoot(string? explicitRoot = null)
    {
        if (!string.IsNullOrEmpty(explicitRoot))
        {
            return explicitRoot;
        }

        var envRoot = Environment.GetEnvironmentVariable("PERLBREW_ROOT");
        if (!string.IsNullOrEmpty(envRoot))
        {
            return envRoot;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "perl5", "perlbrew");
    }

    /// <summary>
    /// Normalizes a version string to perlbrew's naming convention.
    /// Accepts both <c>"5.38.0"</c> and <c>"perl-5.38.0"</c>.
    /// </summary>
    /// <param name="version">The version string to normalize.</param>
    /// <returns>The normalized version string (e.g., <c>perl-5.38.0</c>).</returns>
    public static string NormalizeVersion(string version)
    {
        // Always emit a canonical lowercase "perl-" prefix so the path resolves to
        // the correct directory under $PERLBREW_ROOT/perls/.  Perlbrew installs every
        // version under the lowercase name regardless of what the user typed.
        if (version.StartsWith("perl-", StringComparison.OrdinalIgnoreCase))
        {
            return $"perl-{version["perl-".Length..]}";
        }

        return $"perl-{version}";
    }
}
