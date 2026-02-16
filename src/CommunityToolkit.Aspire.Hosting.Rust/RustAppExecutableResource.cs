namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Rust application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
/// <param name="command">The command to use for the Rust application. Defaults to "cargo".</param>
public class RustAppExecutableResource(string name, string workingDirectory, string command = "cargo")
    : ExecutableResource(name, command, workingDirectory), IResourceWithServiceDiscovery
{

}
