namespace Aspire.Contribs.Hosting.Java;

/// <summary>
/// This represents the options entity for configuring an executable Java application.
/// </summary>
public class JavaAppExecutableResourceOptions
{
    /// <summary>
    /// Gets or sets the application name. Default is <c>target/app.jar</c>.
    /// </summary>
    public string? ApplicationName { get; set; } = "target/app.jar";

    /// <summary>
    /// Gets or sets the port number. Default is <c>8080</c>.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Gets or sets the OpenTelemetry Java Agent path. It should be an absolute path or relative to the working directory.
    /// </summary>
    public string? OtelAgentPath { get; set; } = null;

    /// <summary>
    /// Gets or sets the arguments to pass to the Java application.
    /// </summary>
    public string[]? Args { get; set; } = null;
}
