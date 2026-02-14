namespace Aspire.Hosting;

/// <summary>
/// Represents the options for configuring a Gradle build step.
/// </summary>
public sealed class GradleOptions : JavaBuildOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GradleOptions"/> class.
    /// </summary>
    public GradleOptions()
    {
        Command = "gradlew";
        Args = ["--quiet", "clean", "build"];
    }
}
