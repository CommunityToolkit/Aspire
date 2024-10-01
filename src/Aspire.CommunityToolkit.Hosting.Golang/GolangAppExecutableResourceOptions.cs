namespace Aspire.CommunityToolkit.Hosting.Golang;

/// <summary>
/// This represents the options entity for configuring an executable Golang application.
/// </summary>
public class GolangAppExecutableResourceOptions
{
    /// <summary>
    /// Gets or sets the port number. Default is <c>8080</c>.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Gets or sets the OpenTelemetry Golang Agent path. It should be an absolute path or relative to the working directory.
    /// </summary>
    public string? OtelAgentPath { get; set; } = null;

    /// <summary>
    /// Gets or sets the arguments to pass to the Golang application.
    /// </summary>
    public string[]? Args { get; set; } = null;
}
