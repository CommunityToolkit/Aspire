namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Go module installer that runs go mod tidy or go mod download.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
[Obsolete("Replaced the Aspire.Hosting.Go package. This type will be removed in a future version.")]
public class GoModInstallerResource(string name, string workingDirectory)
    : ExecutableResource(name, "go", workingDirectory);
