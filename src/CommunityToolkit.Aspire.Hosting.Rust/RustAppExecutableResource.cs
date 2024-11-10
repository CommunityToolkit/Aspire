namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Rust application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
public class RustAppExecutableResource(string name, string workingDirectory)
    : ExecutableResource(name, "cargo", workingDirectory), IResourceWithServiceDiscovery
{
    internal const string HttpEndpointName = "http";
}
