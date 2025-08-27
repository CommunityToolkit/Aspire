using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Npgsql;

namespace CommunityToolkit.Aspire.Keycloak.Extensions.Postgres;

/// <summary>
/// Provides extension methods for integrating Keycloak resources with PostgreSQL.
/// </summary>
public static class KeycloakPostgresExtension
{
    /// Configures a Keycloak resource to use a Postgres database by setting appropriate environment variables.
    /// <param name="builder">
    /// The resource builder for the Keycloak resource.
    /// </param>
    /// <param name="database">
    /// The resource builder for the Postgres database resource.
    /// </param>
    /// <param name="app">
    /// The distributed application builder for adding parameters like username and password for the Postgres database.
    /// </param>
    /// <returns>
    /// The updated resource builder for the Keycloak resource.
    /// </returns>
    private static void WithPostgres(this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        PostgresServerResource pgServer = database.Resource.Parent;
        EndpointReference ep = pgServer.GetEndpoint("tcp");

        string dbName = database.Resource.Name;

        ReferenceExpression jdbcUrl = ReferenceExpression.Create(
            $"jdbc:postgresql://{ep.Property(EndpointProperty.Host)}:" +
            $"{ep.Property(EndpointProperty.Port)}/{dbName}");

        builder.WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_URL", jdbcUrl);
    }

    /// Configures a Keycloak resource to use a Postgres database in a development environment by setting appropriate
    /// environment variables and custom connection details.
    /// <param name="builder">
    /// The resource builder for the Keycloak resource.
    /// </param>
    /// <param name="database">
    /// The resource builder for the Postgres database resource.
    /// </param>
    /// <param name="port">
    /// The port for connecting to the Postgres database. Defaults to 5432.
    /// </param>
    /// <returns>
    /// The updated resource builder for the Keycloak resource.
    /// </returns>
    public static IResourceBuilder<KeycloakResource> WithPostgresDev(this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database, int port = 5432)
    {
        WithPostgres(builder, database);

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
        return builder;
    }


    /// Configures a Keycloak resource to use a Postgres database by setting appropriate environment variables, including credentials.
    /// <param name="builder">
    /// The resource builder for the Keycloak resource.
    /// </param>
    /// <param name="database">
    /// The resource builder for the Postgres database resource.
    /// </param>
    /// <param name="username">
    /// The parameter resource representing the username for the Postgres database.
    /// </param>
    /// <param name="password">
    /// The parameter resource representing the password for the Postgres database.
    /// </param>
    /// <returns>
    /// The updated resource builder for the Keycloak resource.
    /// </returns>
    public static IResourceBuilder<KeycloakResource> WithPostgres(this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database, ParameterResource username, ParameterResource password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        WithPostgres(builder, database);
        builder.WithEnvironment("KC_DB_USERNAME", username)
            .WithEnvironment("KC_DB_PASSWORD", password);
        return builder;
    }
}