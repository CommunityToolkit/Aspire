namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a package installer for a perl module.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="packageManager">The package manager to use for the installation, typically CPAN, CPANM (Cpan Minus), or Carton.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
public class PerlModuleInstallerResource(string name, string packageManager, string workingDirectory)
    : ExecutableResource(name, packageManager, workingDirectory);