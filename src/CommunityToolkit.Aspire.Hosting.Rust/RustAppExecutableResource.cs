namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Rust application run as a local executable.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
/// <param name="command">The executable used to run the Rust application. <c>AddBaconApp</c> specifies Bacon explicitly.</param>
[AspireExport(ExposeProperties = true)]
public class RustAppExecutableResource(string name, string workingDirectory, string command = "cargo")
    : ExecutableResource(name, command, workingDirectory), IResourceWithServiceDiscovery, IContainerFilesDestinationResource
{
    /// <inheritdoc />
    public override string ResourceTypeDisplayName => "Rust";
}
