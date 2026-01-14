namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Aspire resource for Flyway database migration tool.
/// </summary>
/// <param name="name">The name of the Flyway resource.</param>
public sealed class FlywayResource([ResourceName] string name) : ContainerResource(name)
{
    /// <summary>
    /// The migration scripts directory inside the Flyway container.
    /// </summary>
    internal const string MigrationScriptsDirectory = "/flyway/sql";
}
