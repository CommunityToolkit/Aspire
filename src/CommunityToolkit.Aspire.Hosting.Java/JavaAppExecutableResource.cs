namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Java application.
/// </summary>
public class JavaAppExecutableResource
    : ExecutableResource, IResourceWithServiceDiscovery, IResourceWithWaitSupport
{
    internal const string HttpEndpointName = "http";

    /// <summary>
    /// Initializes a new instance of the <see cref="JavaAppExecutableResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command.</param>
    public JavaAppExecutableResource(string name, string workingDirectory)
        : base(name, "java", workingDirectory)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JavaAppExecutableResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="workingDirectory">The working directory to use for the command.</param>
    [Obsolete("Use JavaAppExecutableResource(string, string) instead. This constructor will be removed in a future version.")]
    public JavaAppExecutableResource(string name, string command, string workingDirectory)
        : base(name, command, workingDirectory)
    {
    }

    /// <summary>
    /// Gets or sets the path to the JAR file to execute.
    /// </summary>
    public string? JarPath { get; set; }
}
