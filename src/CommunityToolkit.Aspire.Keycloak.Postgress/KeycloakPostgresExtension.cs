using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Keycloak.Postgress;

/// <summary>
/// Provides extension methods for configuring Keycloak with a Postgres database
/// in a resource builder.
/// </summary>
public static class KeycloakPostgresExtension
{
    /// <summary>
    /// Configures Keycloak to use a Postgres database as its underlying storage
    /// by adding the necessary environment variables to the resource builder.
    /// </summary>
    /// <param name="builder">
    /// The resource builder for the Keycloak resource being configured.
    /// </param>
    /// <param name="database">
    /// The resource builder representing the Postgres database resource to be used.
    /// </param>
    /// <param name="transaction">
    /// A boolean flag indicating whether XA transaction support should be enabled.
    /// Defaults to true.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="builder"/> or <paramref name="database"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the parent resource of the Postgres database is not a valid
    /// Postgres server or when username/password parameters are not defined for the
    /// Postgres server.
    /// </exception>
    public static void AddPostgres(this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database, bool transaction = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        if (database.Resource.Parent is null)
            throw new ArgumentException("The Postgres database must be hosted by a Postgres server.",
                nameof(database.Resource.Parent));
        if (database.Resource.Parent.UserNameParameter is null)
            throw new ArgumentException("The Postgres database must have a username parameter.",
                nameof(database.Resource.Parent.UserNameParameter));
        if (database.Resource.Parent.PasswordParameter is null)
            throw new ArgumentException("The Postgres database must have a password parameter.",
                nameof(database.Resource.Parent.PasswordParameter));

        builder.WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_URL", database.Resource.ConnectionStringExpression)
            .WithEnvironment("KC_DB_USERNAME", database.Resource.Parent.UserNameParameter.ValueExpression)
            .WithEnvironment("KC_DB_PASSWORD", database.Resource.Parent.PasswordParameter.ValueExpression)
            .WithEnvironment("KC_TRANSACTION_XA_ENABLED", transaction.ToString().ToLowerInvariant());
    }
}