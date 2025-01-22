namespace Aspire.Hosting;

/// <summary>
/// Represents the options for configuring a Maven build step.
/// </summary>
public sealed class MavenOptions
{
    /// <summary>
    /// Gets or sets the working directory to use for the command. If null, the working directory of the current process is used.
    /// </summary>
    public string? WorkingDirectory { get; set; }
    /// <summary>
    /// Gets or sets the command to execute. Default is "mvnw".
    /// </summary>
    public string Command { get; set; } = "mvnw";
    /// <summary>
    /// Gets or sets the arguments to pass to the command. Default is "--quiet clean package".
    /// </summary>
    public string[] Args { get; set; } = ["--quiet", "clean", "package"];
}