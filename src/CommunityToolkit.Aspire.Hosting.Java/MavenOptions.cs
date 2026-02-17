namespace Aspire.Hosting;

/// <summary>
/// Represents the options for configuring a Maven build step.
/// </summary>
[Obsolete("This class will be removed in a future version.")]
public sealed class MavenOptions : JavaBuildOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MavenOptions"/> class.
    /// </summary>
    public MavenOptions()
    {
        Command = "mvnw";
        Args = ["--quiet", "clean", "package"];
    }
}
