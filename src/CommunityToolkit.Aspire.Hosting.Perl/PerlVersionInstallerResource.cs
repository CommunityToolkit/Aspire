namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that installs a perlbrew-managed Perl version.
/// </summary>
/// <param name="name">The resource name.</param>
/// <param name="workingDirectory">The working directory used for the installer command.</param>
public sealed class PerlVersionInstallerResource(string name, string workingDirectory)
    : ExecutableResource(name, "perlbrew", workingDirectory);
