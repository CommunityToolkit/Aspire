using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using YamlDotNet.Core.Tokens;

namespace CommunityToolkit.Aspire.Keycloak.Extensions;

/// <summary>
/// Provides extension methods for integrating Keycloak resources with PostgreSQL.
/// </summary>
public static class KeycloakPostgresExtension
{
    private static IResourceBuilder<KeycloakResource> WithPostgresData(
        this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database, bool xaEnabled = false)
    {
        PostgresServerResource pgServer = database.Resource.Parent;
        EndpointReference ep = pgServer.GetEndpoint("tcp");

        string dbName = database.Resource.Name;

        ReferenceExpression jdbcUrl = ReferenceExpression.Create(
            $"jdbc:postgresql://{ep.Property(EndpointProperty.Host)}:" +
            $"{ep.Property(EndpointProperty.Port)}/{dbName}");

        builder.WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_URL", jdbcUrl);
        if (xaEnabled)
            builder.WithEnvironment("KC_TRANSACTION_XA_ENABLED", "true");

        return builder;
    }


    /// <summary>
    /// Configures a Keycloak resource to use a PostgreSQL database by setting the necessary
    /// environment variables and dependencies.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="IResourceBuilder{KeycloakResource}"/> instance for configuring the Keycloak resource.
    /// </param>
    /// <param name="database">
    /// The <see cref="IResourceBuilder{PostgresDatabaseResource}"/> instance representing the PostgreSQL database resource.
    /// </param>
    /// <param name="username">
    /// The <see cref="IResourceBuilder{ParameterResource}"/> instance representing the database username parameter resource.
    /// </param>
    /// <param name="password">
    /// The <see cref="IResourceBuilder{ParameterResource}"/> instance representing the database password parameter resource.
    /// </param>
    /// <param name="xaEnabled">
    /// A boolean indicating if XA transactions should be enabled. Defaults to <c>false</c>.
    /// </param>
    /// <returns>
    /// A configured <see cref="IResourceBuilder{KeycloakResource}"/> instance.
    /// </returns>
    public static IResourceBuilder<KeycloakResource> WithPostgres(this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database, IResourceBuilder<ParameterResource> username,
        IResourceBuilder<ParameterResource> password,
        bool xaEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        return WithPostgresData(builder, database, xaEnabled)
            .WithEnvironment("KC_DB_USERNAME", username.Resource)
            .WithEnvironment("KC_DB_PASSWORD", password.Resource);
    }


    /// <summary>
    /// Configures a Keycloak resource to use a PostgreSQL database by setting the necessary
    /// environment variables and dependencies, with default credentials if not provided in the associated database parameters.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="IResourceBuilder{KeycloakResource}"/> instance for configuring the Keycloak resource.
    /// </param>
    /// <param name="database">
    /// The <see cref="IResourceBuilder{PostgresDatabaseResource}"/> instance representing the PostgreSQL database resource.
    /// </param>
    /// <param name="xaEnabled">
    /// A boolean indicating if XA transactions should be enabled. Defaults to <c>false</c>.
    /// </param>
    /// <returns>
    /// A configured <see cref="IResourceBuilder{KeycloakResource}"/> instance.
    /// </returns>
    public static IResourceBuilder<KeycloakResource> WithPostgres(this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database, bool xaEnabled = false)
    {
        var usernameParameter = database.Resource.Parent.UserNameParameter is null
            ? ReferenceExpression.Create($"postgres")
            : ReferenceExpression.Create($"{database.Resource.Parent.UserNameParameter}");
        var passwordParameter = database.Resource.Parent.PasswordParameter is null
            ? ReferenceExpression.Create($"changeme")
            : ReferenceExpression.Create($"{database.Resource.Parent.PasswordParameter}");

        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);
        return WithPostgresData(builder, database, xaEnabled)
            .WithEnvironment("KC_DB_USERNAME", usernameParameter)
            .WithEnvironment("KC_DB_PASSWORD", passwordParameter);
    }
}