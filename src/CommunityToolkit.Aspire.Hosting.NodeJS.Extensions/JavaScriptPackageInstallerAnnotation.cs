using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Represents an annotation for a JavaScript installer resource.
/// </summary>
public sealed class JavaScriptPackageInstallerAnnotation(ExecutableResource installerResource) : IResourceAnnotation
{
    /// <summary>
    /// The instance of the Installer resource used.
    /// </summary>
    public ExecutableResource Resource { get; } = installerResource;
}