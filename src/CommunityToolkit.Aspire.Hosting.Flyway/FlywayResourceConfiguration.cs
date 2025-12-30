namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Flyway resource configuration.
/// </summary>
public sealed record FlywayResourceConfiguration
{
    /// <summary>
    /// Docker image name.
    /// </summary>
    public string ImageName { get; init; } = "flyway/flyway";

    /// <summary>
    /// Docker image tag.
    /// </summary>
    public string ImageTag { get; init; } = "latest";

    /// <summary>
    /// Docker image registry.
    /// </summary>
    public string ImageRegistry { get; init; } = "docker.io";

    /// <summary>
    /// Path to the directory containing Flyway migration scripts.
    /// </summary>
    /// <remarks>
    /// This is an absolute or relative path on the host machine, and must be accessible by Docker.
    /// </remarks>
    public required string MigrationScriptsPath { get; init; }
}
