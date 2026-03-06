using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// Tracks the child resource that ensures cpanm is installed for a perlbrew-managed Perl version.
/// </summary>
/// <param name="installerResource">The installer resource that runs <c>perlbrew install-cpanm</c>.</param>
internal sealed class PerlCpanmInstallerAnnotation(ExecutableResource installerResource) : IResourceAnnotation
{
    /// <summary>
    /// Gets the installer resource that installs cpanm into the active perlbrew version.
    /// </summary>
    public ExecutableResource Resource { get; } = installerResource;
}
