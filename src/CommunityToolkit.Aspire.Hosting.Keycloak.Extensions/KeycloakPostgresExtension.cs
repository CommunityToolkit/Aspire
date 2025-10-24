using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for integrating Keycloak resources with PostgreSQL.
/// </summary>
public static class KeycloakPostgresExtension
{
    private static IResourceBuilder<KeycloakResource> WithPostgresData(
        this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database,
        bool xaEnabled = false)
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

    private static ReferenceExpression ToRef(ParameterResource? value)
    {
        return value is null ? ReferenceExpression.Create($"postgres") : ReferenceExpression.Create($"{value}");
    }

    /// Adds support for Keycloak to connect to a specified Postgres database resource, with optional credentials
    /// and configuration for XA transactions.
    /// <param name="builder">
    /// The resource builder for configuring a Keycloak resource.
    /// </param>
    /// <param name="database">
    /// The resource builder for the Postgres database that Keycloak will connect to.
    /// </param>
    /// <param name="username">
    /// (Optional) The resource builder for the parameter defining the database username.
    /// If not provided, the parent database resource's username parameter will be used.
    /// </param>
    /// <param name="password">
    /// (Optional) The resource builder for the parameter defining the database password.
    /// If not provided, the parent database resource's password parameter will be used.
    /// </param>
    /// <param name="xaEnabled">
    /// A boolean flag indicating whether XA transactions are enabled. Defaults to false.
    /// </param>
    /// <returns>
    /// An updated resource builder with Postgres integration configured for the Keycloak resource.
    /// </returns>
    public static IResourceBuilder<KeycloakResource> WithPostgres(
        this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database,
        IResourceBuilder<ParameterResource>? username = null,
        IResourceBuilder<ParameterResource>? password = null,
        bool xaEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        return WithPostgresData(builder, database, xaEnabled)
            .WithEnvironment("KC_DB_USERNAME", ToRef(username?.Resource ?? 
                                                     database.Resource.Parent.UserNameParameter))
            .WithEnvironment("KC_DB_PASSWORD", ToRef(password?.Resource ?? 
                                                     database.Resource.Parent.PasswordParameter));
    }
}