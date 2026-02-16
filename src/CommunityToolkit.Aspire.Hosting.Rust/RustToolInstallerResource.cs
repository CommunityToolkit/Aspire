namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Rust tool installer using cargo.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
public class RustToolInstallerResource(string name, string workingDirectory)
    : ExecutableResource(name, "cargo", workingDirectory);
