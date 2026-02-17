namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Java application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
public class JavaAppExecutableResource(string name, string workingDirectory)
    : ExecutableResource(name, "java", workingDirectory), IResourceWithServiceDiscovery, IContainerFilesDestinationResource, IResourceWithWaitSupport
{
    internal const string HttpEndpointName = "http";

    /// <inheritdoc/>
    public string? ContainerFilesDestination => "/app/static";

    /// <summary>
    /// Gets or sets the path to the JAR file to execute.
    /// </summary>
    public string JarPath { get; set; } = "target/app.jar";
}
