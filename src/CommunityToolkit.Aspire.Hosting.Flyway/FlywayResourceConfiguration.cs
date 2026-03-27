namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Flyway resource configuration.
/// </summary>
public sealed record FlywayResourceConfiguration
{
    /// <summary>
    /// Path to the directory containing Flyway migration scripts.
    /// </summary>
    /// <remarks>
    /// This is an absolute or relative path on the host machine, and must be accessible by Docker.
    /// </remarks>
    public required string MigrationScriptsPath { get; init; }
}
