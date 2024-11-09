namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Golang application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
public class GolangAppExecutableResource(string name, string workingDirectory)
    : ExecutableResource(name, "go", workingDirectory), IResourceWithServiceDiscovery;