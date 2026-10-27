using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// Links a <see cref="PerlAppResource"/> to its installer child resource.
/// This annotation tracks the <see cref="ExecutableResource"/> that performs the installation.
/// </summary>
/// <param name="installerResource">The child executable resource that runs the install command.</param>
/// <param name="packageName">The original Perl module name requested for installation.</param>
internal sealed class PerlModuleInstallerAnnotation(ExecutableResource installerResource, string packageName) : IResourceAnnotation
{
    public ExecutableResource Resource { get; } = installerResource;

    /// <summary>
    /// Gets the original, case-sensitive Perl module identity.
    /// </summary>
    public string PackageName { get; } = packageName;
}