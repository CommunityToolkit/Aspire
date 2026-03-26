using Aspire.Hosting.ApplicationModel;

#pragma warning disable ASPIREATS001

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring Flyway migrations on a PostgreSQL database resource builder.
/// </summary>
public static partial class PostgresDatabaseResourceBuilderExtensions
{
    extension(IResourceBuilder<PostgresDatabaseResource> builder)
    {
        /// <summary>
        /// Configures the PostgreSQL database resource to run Flyway database migrations using the provided Flyway resource builder.
        /// </summary>
        /// <param name="flywayResourceBuilder">The resource builder used to configure the Flyway resource. Must not be null.</param>
        /// <returns>
        /// An updated resource builder for the PostgreSQL database resource, configured to execute Flyway migrations.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method sets up the necessary Flyway command-line arguments to connect to the PostgreSQL database and run migrations.
        /// The Flyway resource will be configured to use the connection details from the PostgreSQL database resource.
        /// Ensure that the Flyway resource builder is properly initialized before calling this method.
        /// </para>
        /// <para>
        /// This overload is not available in polyglot app hosts. Use the overload that accepts a Flyway resource name and migration scripts path instead.
        /// </para>
        /// <example>
        /// <para>
        /// This example demonstrates how to configure a Flyway migration for a PostgreSQL database using the extension method.
        /// </para>
        /// <code lang="csharp">
        /// var flyway = builder.AddFlyway("flyway", "./migrations");
        /// var postgres = builder.AddPostgres("postgres");
        /// var database = postgres.AddDatabase("database").WithFlywayMigration(flyway);
        /// flyway.WaitFor(database);
        /// </code>
        /// </example>
        /// </remarks>
        [AspireExportIgnore(Reason = "IResourceBuilder<FlywayResource> cannot be created from polyglot app hosts. Use the overload that creates the Flyway resource from a name and migration scripts path instead.")]
        public IResourceBuilder<PostgresDatabaseResource> WithFlywayMigration(IResourceBuilder<FlywayResource> flywayResourceBuilder) =>
            builder.WithFlywayCommand(flywayResourceBuilder, "migrate");

        [AspireExport("withFlywayMigration", Description = "Creates a Flyway resource and configures it to run migrations for the PostgreSQL database.")]
        internal IResourceBuilder<PostgresDatabaseResource> WithFlywayMigrationForPolyglot([ResourceName] string flywayName, string migrationScriptsPath) =>
            builder.WithFlywayMigration(builder.ApplicationBuilder.AddFlyway(flywayName, migrationScriptsPath));

        /// <summary>
        /// Configures the PostgreSQL database resource to run Flyway database migrations repair using the provided Flyway resource builder.
        /// </summary>
        /// <param name="flywayResourceBuilder">The resource builder used to configure the Flyway resource. Must not be null.</param>
        /// <returns>
        /// An updated resource builder for the PostgreSQL database resource, configured to execute Flyway migrations repair.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method sets up the necessary Flyway command-line arguments to connect to the PostgreSQL database and run migrations repair.
        /// The Flyway resource will be configured to use the connection details from the PostgreSQL database resource.
        /// Ensure that the Flyway resource builder is properly initialized before calling this method.
        /// </para>
        /// <para>
        /// This overload is not available in polyglot app hosts. Use the overload that accepts a Flyway resource name and migration scripts path instead.
        /// </para>
        /// <example>
        /// <para>
        /// This example demonstrates how to configure a Flyway migrations repair for a PostgreSQL database using the extension method.
        /// </para>
        /// <code lang="csharp">
        /// var flyway = builder.AddFlyway("flyway", "./migrations").WithExplicitStart();
        /// var postgres = builder.AddPostgres("postgres");
        /// var database = postgres.AddDatabase("database").WithFlywayRepair(flyway);
        /// flyway.WaitFor(database);
        /// </code>
        /// </example>
        /// </remarks>
        [AspireExportIgnore(Reason = "IResourceBuilder<FlywayResource> cannot be created from polyglot app hosts. Use the overload that creates the Flyway resource from a name and migration scripts path instead.")]
        public IResourceBuilder<PostgresDatabaseResource> WithFlywayRepair(IResourceBuilder<FlywayResource> flywayResourceBuilder) =>
            builder.WithFlywayCommand(flywayResourceBuilder, "repair");

        [AspireExport("withFlywayRepair", Description = "Creates a Flyway resource and configures it to run repair for the PostgreSQL database.")]
        internal IResourceBuilder<PostgresDatabaseResource> WithFlywayRepairForPolyglot([ResourceName] string flywayName, string migrationScriptsPath) =>
            builder.WithFlywayRepair(builder.ApplicationBuilder.AddFlyway(flywayName, migrationScriptsPath));

        private IResourceBuilder<PostgresDatabaseResource> WithFlywayCommand(IResourceBuilder<FlywayResource> flywayResourceBuilder, string command)
        {
            ArgumentNullException.ThrowIfNull(flywayResourceBuilder);

            var postgresServerResource = builder.Resource.Parent;
            var host = postgresServerResource.PrimaryEndpoint.Property(EndpointProperty.Host);
            var port = postgresServerResource.PrimaryEndpoint.Property(EndpointProperty.TargetPort);

            flywayResourceBuilder.WithArgs(
                context =>
                {
                    context.Args.Add(ReferenceExpression.Create($"-url=jdbc:postgresql://{host}:{port}/{builder.Resource.DatabaseName}"));
                    context.Args.Add(ReferenceExpression.Create($"-user={postgresServerResource.UserNameReference}"));
                    context.Args.Add(ReferenceExpression.Create($"-password={postgresServerResource.PasswordParameter}"));
                    context.Args.Add(command);
                });

            return builder;
        }
    }
}

#pragma warning restore ASPIREATS001
