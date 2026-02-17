namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Java application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
/// <param name="entrypoint">An optional container entrypoint.</param>

public class JavaAppContainerResource(string name, string workingDirectory, string? entrypoint = null)
    : ContainerResource(name, entrypoint), IResourceWithServiceDiscovery, IResourceWithWaitSupport
{
    internal const string HttpEndpointName = "http";

    /// <inheritdoc/>
    public string WorkingDirectory { get; } = workingDirectory;
}
