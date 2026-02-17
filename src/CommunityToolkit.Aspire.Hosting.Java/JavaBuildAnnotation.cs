namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a metadata annotation that specifies Java build options for publishing.
/// </summary>
/// <param name="buildImage">The build image to use.</param>
/// <param name="buildCommand">The build command to run.</param>
internal sealed class JavaBuildAnnotation(string buildImage, string buildCommand) : IResourceAnnotation
{
    /// <summary>
    /// The image to use for building the Java application.
    /// </summary>
    public string BuildImage { get; } = buildImage;

    /// <summary>
    /// The command to run to build the Java application.
    /// </summary>
    public string BuildCommand { get; } = buildCommand;
}
