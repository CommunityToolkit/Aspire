namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Maven build step.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="wrapperScript">The full path to the Maven wrapper script.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
public class MavenBuildResource(string name, string wrapperScript, string workingDirectory)
    : ExecutableResource(name, wrapperScript, workingDirectory);
