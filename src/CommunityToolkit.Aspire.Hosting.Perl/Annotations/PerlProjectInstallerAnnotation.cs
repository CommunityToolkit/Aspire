using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// Links a <see cref="PerlAppResource"/> to its project-level dependency installer.
/// Distinct from <see cref="PerlModuleInstallerAnnotation"/>, which tracks per-package installers.
/// This annotation marks the installer that runs project-wide dependency resolution
/// (e.g., <c>cpanm --installdeps .</c> or <c>carton install</c>).
/// </summary>
/// <param name="installerResource">The child executable resource that runs the project-level install command.</param>
internal sealed class PerlProjectInstallerAnnotation(ExecutableResource installerResource) : IResourceAnnotation
{
    /// <summary>
    /// Gets the child resource that performs the project-level dependency installation.
    /// </summary>
    public ExecutableResource Resource { get; } = installerResource;
}
