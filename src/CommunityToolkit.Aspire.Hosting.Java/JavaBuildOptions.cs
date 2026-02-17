namespace Aspire.Hosting;

/// <summary>
/// Represents the base options for configuring a Java build step.
/// </summary>
[Obsolete("This class will be removed in a future version.")]
public abstract class JavaBuildOptions
{
    /// <summary>
    /// Gets or sets the working directory to use for the command. If null, the working directory of the current process is used.
    /// </summary>
    public string? WorkingDirectory { get; set; }
    /// <summary>
    /// Gets or sets the command to execute.
    /// </summary>
    public string Command { get; set; } = default!;
    /// <summary>
    /// Gets or sets the arguments to pass to the command.
    /// </summary>
    public string[] Args { get; set; } = [];
}
