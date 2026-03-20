namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents publish-time metadata for building a Java application inside a container image.
/// </summary>
/// <param name="tool">The build tool used during publish.</param>
/// <param name="wrapperPath">The configured wrapper path, or <c>null</c> to use the default wrapper.</param>
/// <param name="args">The arguments to pass to the publish build command.</param>
internal sealed class JavaPublishBuildAnnotation(JavaBuildTool tool, string? wrapperPath, string[] args) : IResourceAnnotation
{
    /// <summary>
    /// The build tool used during publish.
    /// </summary>
    public JavaBuildTool Tool { get; } = tool;

    /// <summary>
    /// The configured wrapper path, or <c>null</c> to use the default wrapper for the tool.
    /// </summary>
    public string? WrapperPath { get; } = wrapperPath;

    /// <summary>
    /// The arguments to pass to the publish build command.
    /// </summary>
    public string[] Args { get; } = args;
}
