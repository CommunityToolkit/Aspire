namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Represents the settings for the DuckDB client.
/// </summary>
public sealed class DuckDBConnectionSettings
{
    /// <summary>
    /// The connection string of the DuckDB database to connect to.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the database health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }
}
