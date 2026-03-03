using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// Stores the CLI arguments for a Perl module install command.
/// Distinct from <see cref="PerlModuleInstallerAnnotation"/>, which links to the installer resource.
/// </summary>
/// <param name="args">
/// The command line arguments for the Perl package manager's install command.
/// This includes the command itself (e.g., "install", "--force", "ModuleName").
/// </param>

internal sealed class PerlModuleInstallCommandAnnotation(string[] args) : IResourceAnnotation
{
    /// <summary>
    /// Gets the command line arguments for the Perl module install command.
    /// This includes the command itself (i.e. "install", "sync").
    /// 
    /// Can be affected by _which_ package manager is being used (e.g., cpan, cpanm, etc.)
    /// </summary>
    public string[] Args { get; } = args;
}