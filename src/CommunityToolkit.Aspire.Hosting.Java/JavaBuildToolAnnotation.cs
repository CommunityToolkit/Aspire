namespace Aspire.Hosting.ApplicationModel;

internal enum JavaBuildTool
{
    Maven,
    Gradle
}

/// <summary>
/// Represents a metadata annotation that specifies the build tool used to run the Java application.
/// </summary>
/// <param name="tool">The build tool used to run the Java application.</param>
/// <param name="wrapperPath">The full path to the build tool wrapper script.</param>
/// <param name="args">The arguments to pass to the build tool (e.g., the goal or task name).</param>
internal sealed class JavaBuildToolAnnotation(JavaBuildTool tool, string wrapperPath, string[] args) : IResourceAnnotation
{
    /// <summary>
    /// The build tool used to run the Java application.
    /// </summary>
    public JavaBuildTool Tool { get; } = tool;

    /// <summary>
    /// The full path to the build tool wrapper script (e.g., mvnw or gradlew).
    /// </summary>
    public string WrapperPath { get; } = wrapperPath;

    /// <summary>
    /// The arguments to pass to the build tool.
    /// </summary>
    public string[] Args { get; } = args;
}
