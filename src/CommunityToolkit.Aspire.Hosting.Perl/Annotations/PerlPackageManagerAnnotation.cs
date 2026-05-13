using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// Represents the annotation for the Perl package manager used in a resource.
/// </summary>
/// <param name="packageManager">The package manager to use for module installation.</param>
internal sealed class PerlPackageManagerAnnotation(PerlPackageManager packageManager) : IResourceAnnotation
{
    /// <summary>
    /// Gets the package manager variant.
    /// </summary>
    public PerlPackageManager PackageManager { get; } = packageManager;

    /// <summary>
    /// Gets the executable name used to invoke this package manager on the command line.
    /// </summary>
    public string ExecutableName => PackageManager.ToExecutableName();
}