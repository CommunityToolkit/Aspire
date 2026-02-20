namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Gradle build step.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory to use for the command.</param>
public class GradleBuildResource(string name, string workingDirectory)
    : ExecutableResource(name, "gradlew", workingDirectory);
