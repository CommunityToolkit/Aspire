namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Represents the settings for the Sqlite client.
/// </summary>
public sealed class SqliteEntityFrameworkCoreSettings
{
    /// <summary>
    /// The connection string of the PostgreSQL database to connect to.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the database health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets the default timeout for the database operations.
    /// </summary>
    public int DefaultTimeout { get; set; }
}