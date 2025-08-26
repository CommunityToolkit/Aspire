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
    /// Configures Keycloak to use a Postgres database by setting up the necessary
    /// environment variables and configurations through the provided resource builders.
    /// </summary>
    /// <param name="builder">The resource builder for configuring the Keycloak resource.</param>
    /// <param name="database">The resource builder for the Postgres database resource.</param>
    /// <param name="transaction">
    /// A boolean indicating whether transactions are enabled. Default value is <c>true</c>.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token used to propagate the notification that the operation should be canceled.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous configuration operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="builder"/> or <paramref name="database"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if the Postgres database resource does not have a parent,
    /// or if the parent does not have necessary username or password parameters.
    /// </exception>
    public static async Task AddPostgrctes(this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database, bool transaction = true,
        CancellationToken cancellationToken = default)
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
            .WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_URL_HOST", database.Resource.Parent.PrimaryEndpoint.Host)
            .WithEnvironment("KC_DB_URL_PORT", database.Resource.Parent.PrimaryEndpoint.Port.ToString())
            .WithEnvironment("KC_DB_DATABASE", database.Resource.Name)
            .WithEnvironment("KC_DB_USERNAME",
                await database.Resource.Parent.UserNameParameter.GetValueAsync(cancellationToken))
            .WithEnvironment("KC_DB_PASSWORD",
                await database.Resource.Parent.PasswordParameter.GetValueAsync(cancellationToken))
            .WithEnvironment("KC_TRANSACTION_XA_ENABLED", true.ToString().ToLowerInvariant());
    }
}