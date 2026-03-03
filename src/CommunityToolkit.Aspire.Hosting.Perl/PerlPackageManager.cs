namespace CommunityToolkit.Aspire.Hosting.Perl;

/// <summary>
/// Identifies the Perl package manager used for module installation.
/// </summary>
public enum PerlPackageManager
{
    /// <summary>
    /// The standard CPAN client (<c>cpan</c>). Default for all Perl resources.
    /// </summary>
    Cpan,

    /// <summary>
    /// App::cpanminus (<c>cpanm</c>). A lightweight alternative to the full CPAN client.
    /// Enabled via <c>WithCpanMinus()</c>.
    /// </summary>
    Cpanm,

    /// <summary>
    /// Carton — a Perl module dependency manager using <c>cpanfile</c> and lock files.
    /// Enabled via <c>WithCarton()</c>.
    /// </summary>
    Carton,
}

/// <summary>
/// Extension methods for <see cref="PerlPackageManager"/>.
/// </summary>
public static class PerlPackageManagerExtensions
{
    /// <summary>
    /// Gets the executable name used to invoke this package manager on the command line.
    /// </summary>
    public static string ToExecutableName(this PerlPackageManager packageManager) => packageManager switch
    {
        PerlPackageManager.Cpan => "cpan",
        PerlPackageManager.Cpanm => "cpanm",
        PerlPackageManager.Carton => "carton",
        _ => throw new NotSupportedException($"Package manager '{packageManager}' is not supported.")
    };
}
