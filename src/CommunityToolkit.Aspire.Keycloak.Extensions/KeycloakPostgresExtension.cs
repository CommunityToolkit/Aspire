using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Keycloak.Extensions;

/// <summary>
/// Provides extension methods for integrating Keycloak resources with PostgreSQL.
/// </summary>
public static class KeycloakPostgresExtension
{
    /// <summary>
    /// Configures a Keycloak resource to use a PostgreSQL database.
    /// Sets up the necessary environment variables for Keycloak to connect to the specified PostgreSQL database.
    /// </summary>
    /// <param name="builder">
    /// The resource builder for the Keycloak resource to be configured.
    /// </param>
    /// <param name="database">
    /// The resource builder for the PostgreSQL database resource that the Keycloak resource will connect to.
    /// </param>
    /// <param name="username">
    /// (Optional) The resource builder for the database username parameter.
    /// </param>
    /// <param name="password">
    /// (Optional) The resource builder for the database password parameter.
    /// </param>
    /// <param name="xaEnabled">
    /// Indicates whether XA transactions are enabled for the database connection.
    /// </param>
    public static IResourceBuilder<KeycloakResource> WithPostgres(
        this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database, IResourceBuilder<ParameterResource> username,
        IResourceBuilder<ParameterResource> password,
        bool xaEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        EndpointReference ep = database.Resource.Parent.GetEndpoint("tcp");

        string dbName = database.Resource.Name;

        ReferenceExpression jdbcUrl = ReferenceExpression.Create(
            $"jdbc:postgresql://{ep.Property(EndpointProperty.Host)}:" +
            $"{ep.Property(EndpointProperty.Port)}/{dbName}");

        builder.WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_URL", jdbcUrl);

        builder.WithEnvironment("KC_DB_USERNAME", username.Resource);
        builder.WithEnvironment("KC_DB_PASSWORD", password.Resource);

        if (xaEnabled)
            builder.WithEnvironment("KC_TRANSACTION_XA_ENABLED", "true");

        return builder;
    }


    /// <summary>
    /// Configures a <see cref="KeycloakResource"/> to use a PostgreSQL database
    /// by automatically creating the required parameters and database resources
    /// within the distributed application builder.
    /// </summary>
    /// <param name="builder">
    /// The resource builder for the Keycloak instance to be configured.
    /// </param>
    /// <param name="appBuilder">
    /// The distributed application builder used to register parameters and
    /// the PostgreSQL database resource.
    /// </param>
    /// <param name="xaEnabled">
    /// Indicates whether XA transactions should be enabled for the database
    /// connection. Default is <c>false</c>.
    /// </param>
    /// <param name="usernameParameter">
    /// The name of the Keycloak database username parameter. Default is
    /// <c>"keycloak-username"</c>.
    /// </param>
    /// <param name="databaseName">
    /// The name of the PostgreSQL database to be created for Keycloak.
    /// Default is <c>"keycloak-db"</c>.
    /// </param>
    /// <param name="postgrsName">
    /// The logical name of the PostgreSQL server resource. Default is
    /// <c>"keycloak-postgres"</c>.
    /// </param>
    /// <returns>
    /// The updated <see cref="IResourceBuilder{KeycloakResource}"/> configured
    /// with the PostgreSQL integration.
    /// </returns>

    public static IResourceBuilder<KeycloakResource> WithPostgres(this IResourceBuilder<KeycloakResource> builder,
        IDistributedApplicationBuilder appBuilder, bool xaEnabled = false,
        string usernameParameter = "keycloak-username", string databaseName = "keycloak-db",
        string postgrsName = "keycloak-postgres")
    {
        var username = appBuilder.AddParameter(usernameParameter, "keycloak-db-user");
        var pwd = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(appBuilder, "pg-pass");
        var password = appBuilder.CreateResourceBuilder(pwd);
        var keycloakPostgres = appBuilder.AddPostgres(postgrsName, username, password);
        var db = keycloakPostgres.AddDatabase(databaseName);

        builder.WithPostgres(db, username, password, xaEnabled)
            .WithReference(db)
            .WaitFor(keycloakPostgres);
        return builder;
    }
}