using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// Links a <see cref="PerlAppResource"/> to its installer child resource.
/// Distinct from <see cref="PerlModuleInstallCommandAnnotation"/>, which stores CLI args.
/// This annotation tracks the <see cref="ExecutableResource"/> that performs the installation.
/// </summary>
/// <param name="installerResource">The child executable resource that runs the install command.</param>
internal sealed class PerlModuleInstallerAnnotation(ExecutableResource installerResource) : IResourceAnnotation
{

    public ExecutableResource Resource { get; } = installerResource;
}