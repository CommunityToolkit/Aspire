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
    /// Configure Keycloak to use PostgreSQL with explicit username/password parameters.
    /// </summary>
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
    /// Configure Keycloak to use PostgreSQL, falling back to default credentials if server parameters are not provided.
    /// </summary>
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