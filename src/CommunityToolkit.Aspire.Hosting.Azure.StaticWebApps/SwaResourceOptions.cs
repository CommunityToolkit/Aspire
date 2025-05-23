namespace Aspire.Hosting;

/// <summary>
/// Represents the configuration options for a Static Web App resource.
/// </summary>
public class SwaResourceOptions
{
    /// <summary>
    /// Gets or sets the port number on which the Static Web App will run.
    /// Default value is 4280.
    /// </summary>
    public int Port { get; set; } = Random.Shared.Next(4280, 5280);

    /// <summary>
    /// Gets or sets the timeout duration (in seconds) for the development server.
    /// Default value is 60 seconds.
    /// </summary>
    public int DevServerTimeout { get; set; } = 60;
}