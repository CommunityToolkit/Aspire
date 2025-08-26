using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Npgsql;

namespace CommunityToolkit.Aspire.Keycloak.Postgress;

/// <summary>
/// Provides extension methods for configuring Keycloak with a Postgres database
/// in a resource builder.
/// </summary>
public static class KeycloakPostgresExtension
{
    /// <summary>
    /// Configures Keycloak with a Postgres database using the provided resource builders.
    /// This method sets environment variables required for Keycloak to connect to PostgreSQL.
    /// </summary>
    /// <param name="builder">
    /// The resource builder for the Keycloak resource. It is required for defining Keycloak settings.
    /// </param>
    /// <param name="database">
    /// The resource builder for the Postgres database resource. It provides connection string
    /// information and other database-related configurations.
    /// </param>
    /// <param name="transaction">
    /// A boolean indicating whether transactions should be enabled in the configuration. Default is true.
    /// </param>
    /// <param name="port">
    /// The port number on which the Postgres database is running. Default is 5432.
    /// </param>
    public static void AddPostgrctes(this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database, bool transaction = true, int port = 5432)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);


        database.OnConnectionStringAvailable(async (dataResource, _, cancellationToken) =>
        {
            NpgsqlConnectionStringBuilder npg =
                new(await dataResource.ConnectionStringExpression.GetValueAsync(cancellationToken));
            builder.WithEnvironment("KC_DB", "postgres")
                .WithEnvironment("KC_DB", "postgres")
                .WithEnvironment("KC_DB_URL",
                    $"jdbc:postgresql://{dataResource.Parent.Name}:{port.ToString()}/{npg.Database}")
                .WithEnvironment("KC_DB_USERNAME", npg.Username)
                .WithEnvironment("KC_DB_PASSWORD", npg.Password);
        });
    }
}