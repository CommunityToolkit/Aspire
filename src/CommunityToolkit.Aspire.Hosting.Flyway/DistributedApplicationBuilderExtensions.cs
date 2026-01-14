using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods to support adding Flyway to the <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class DistributedApplicationBuilderExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds a Flyway resource to the application with default configuration.
        /// </summary>
        /// <param name="name">The name of the Flyway resource.</param>
        /// <param name="migrationScriptsPath">The path to the directory containing Flyway migration scripts.</param>
        /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
        /// <remarks>
        /// <para>
        /// <paramref name="migrationScriptsPath"/> is an absolute or relative path on the host machine, and must be accessible by Docker.
        /// </para>
        /// <para>
        /// This method is meant to be used in conjunction with a database resource added to the application and the Flyway extension built for that database resource.
        /// For example, if adding a PostgreSQL database resource, the Flyway PostgreSQL extension can be used to configure the Flyway resource to perform migrations against that database.
        /// </para>
        /// <example>
        /// This example shows how to add a Flyway resource with migration scripts located in the "./migrations" directory.
        /// <code lang="csharp">
        /// var flywayMigration = builder.AddFlyway("flywayMigration", "./migrations");
        /// </code>
        /// </example>
        /// </remarks>
        public IResourceBuilder<FlywayResource> AddFlyway([ResourceName] string name, string migrationScriptsPath)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(migrationScriptsPath);

            var resource = new FlywayResource(name);

            var flywayResourceBuilder = builder
                .AddResource(resource)
                .WithImage(FlywayContainerImageTags.Image)
                .WithImageTag(FlywayContainerImageTags.Tag)
                .WithImageRegistry(FlywayContainerImageTags.Registry)
                .WithEnvironment("FLYWAY_LOCATIONS", $"filesystem:{FlywayResource.MigrationScriptsDirectory}")
                .WithEnvironment("REDGATE_DISABLE_TELEMETRY", "true")
                .WithBindMount(Path.GetFullPath(migrationScriptsPath), FlywayResource.MigrationScriptsDirectory, isReadOnly: true);

            return flywayResourceBuilder;
        }
    }
}
