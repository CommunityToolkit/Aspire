namespace CommunityToolkit.Aspire.Neon;

/// <summary>
/// Represents the settings for the Neon client.
/// </summary>
public sealed class NeonClientSettings
{
    /// <summary>
    /// Gets or sets the connection string used to connect to Neon.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Neon health check is disabled.
    /// </summary>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates the Neon health check timeout in milliseconds.
    /// </summary>
    public int? HealthCheckTimeout { get; set; }
}