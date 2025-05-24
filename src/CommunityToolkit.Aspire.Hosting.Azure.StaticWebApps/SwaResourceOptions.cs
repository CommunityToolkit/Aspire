namespace Aspire.Hosting;

/// <summary>
/// Represents the configuration options for a Static Web App resource.
/// </summary>
[Obsolete(
    message: "The SWA emulator integration is going to be removed in a future release.",
    error: false,
    DiagnosticId = "CTASPIRE003",
    UrlFormat = "https://github.com/CommunityToolit/aspire/issues/698")]
public class SwaResourceOptions
{
    /// <summary>
    /// Gets or sets the port number on which the Static Web App will run.
    /// Default value is 4280.
    /// </summary>
    public int Port { get; set; } = 4280;

    /// <summary>
    /// Gets or sets the timeout duration (in seconds) for the development server.
    /// Default value is 60 seconds.
    /// </summary>
    public int DevServerTimeout { get; set; } = 60;
}