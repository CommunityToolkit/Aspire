using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for integrating Keycloak resources with PostgreSQL.
/// </summary>
public static class KeycloakPostgresExtension
{
    private static IResourceBuilder<KeycloakResource> WithPostgresData(
        this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database, bool xaEnabled = false)
    {
        var pgServer = database.Resource.Parent;
        var ep = pgServer.GetEndpoint("tcp");

        var dbName = database.Resource.Name;

        var jdbcUrl = ReferenceExpression.Create(
            $"jdbc:postgresql://{ep.Property(EndpointProperty.Host)}:{ep.Property(EndpointProperty.Port)}/{dbName}");

        builder.WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_URL", jdbcUrl)
            .WithEnvironment("KC_TRANSACTION_XA_ENABLED", xaEnabled.ToString().ToLower())
            .WaitFor(database);

        return builder;
    }


    /// <summary>
    /// Configures a Keycloak resource to use PostgreSQL, explicitly setting credentials and other related values.
    /// </summary>
    /// <param name="builder">The builder for the Keycloak resource.</param>
    /// <param name="database">The builder for the PostgreSQL database resource to use for Keycloak.</param>
    /// <param name="username">The builder for the parameter resource representing the username for PostgreSQL authentication.</param>
    /// <param name="password">The builder for the parameter resource representing the password for PostgreSQL authentication.</param>
    /// <param name="xaEnabled">Indicates whether XA transactions are enabled for PostgreSQL. Defaults to false.</param>
    /// <returns>The updated Keycloak resource builder configured with PostgreSQL integration and explicit credentials.</returns>
    public static IResourceBuilder<KeycloakResource> WithPostgres(this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database, IResourceBuilder<ParameterResource> username,
        IResourceBuilder<ParameterResource> password, bool xaEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        return WithPostgresData(builder, database, xaEnabled)
            .WithEnvironment("KC_DB_USERNAME", username.Resource)
            .WithEnvironment("KC_DB_PASSWORD", password.Resource);
    }


    /// <summary>
    /// Configures a Keycloak resource to use a PostgreSQL database, setting credentials and enabling optional XA transactions.
    /// </summary>
    /// <param name="builder">The builder for the Keycloak resource to configure with PostgreSQL.</param>
    /// <param name="database">The builder for the PostgreSQL database resource to integrate with Keycloak.</param>
    /// <param name="xaEnabled">Specifies whether XA transactions are enabled for the PostgreSQL database. Defaults to false.</param>
    /// <returns>The updated Keycloak resource builder configured with PostgreSQL integration.</returns>
    public static IResourceBuilder<KeycloakResource> WithPostgres(
        this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database,
        bool xaEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        var usernameParameter = database.Resource.Parent.UserNameParameter is null
            ? ReferenceExpression.Create($"postgres")
            : ReferenceExpression.Create($"{database.Resource.Parent.UserNameParameter}");

        var passwordParameter = database.Resource.Parent.PasswordParameter is null
            ? ReferenceExpression.Create($"postgres")
            : ReferenceExpression.Create($"{database.Resource.Parent.PasswordParameter}");

        return WithPostgresData(builder, database, xaEnabled)
            .WithEnvironment("KC_DB_USERNAME", usernameParameter)
            .WithEnvironment("KC_DB_PASSWORD", passwordParameter);
    }
}