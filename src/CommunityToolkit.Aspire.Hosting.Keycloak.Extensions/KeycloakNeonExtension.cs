using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for integrating Keycloak resources with Neon Postgres databases.
/// </summary>
public static class KeycloakNeonExtension
{
    /// <summary>
    /// Configures Keycloak to use a Neon database resource for its backing store.
    /// </summary>
    /// <param name="builder">The resource builder for configuring a Keycloak resource.</param>
    /// <param name="database">The Neon database resource to connect to.</param>
    /// <param name="xaEnabled">
    /// A boolean flag indicating whether XA transactions are enabled. Defaults to <see langword="false"/>.
    /// </param>
    /// <param name="schema">
    /// The PostgreSQL schema Keycloak should use for its tables (maps to <c>KC_DB_SCHEMA</c>).
    /// If <see langword="null"/>, Keycloak defaults to the <c>public</c> schema.
    /// Keycloak automatically creates the schema on startup if it does not already exist.
    /// </param>
    /// <returns>An updated resource builder with Neon Postgres integration configured for the Keycloak resource.</returns>
    /// <remarks>
    /// <para>
    /// The connection details are resolved at startup, after the Neon provisioner has completed.
    /// Keycloak will automatically wait for the Neon database resource to be healthy before starting,
    /// which in turn only becomes healthy once the parent Neon project has been fully provisioned.
    /// </para>
    /// <para>
    /// This method sets the following environment variables on the Keycloak container:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>KC_DB</c> — set to <c>postgres</c></description></item>
    /// <item><description><c>KC_DB_URL</c> — JDBC URL built from the Neon endpoint host, port, and database name</description></item>
    /// <item><description><c>KC_DB_USERNAME</c> — the Neon database role name</description></item>
    /// <item><description><c>KC_DB_PASSWORD</c> — the Neon database password</description></item>
    /// <item><description><c>KC_TRANSACTION_XA_ENABLED</c> — <c>true</c> or <c>false</c> based on <paramref name="xaEnabled"/></description></item>
    /// <item><description><c>KC_DB_SCHEMA</c> — only set when <paramref name="schema"/> is provided</description></item>
    /// </list>
    /// <example>
    /// <code lang="csharp">
    /// var neonApiKey = builder.AddParameterFromConfiguration("neonApiKey", "Neon:ApiKey", secret: true);
    /// var neon = builder.AddNeon("neon", neonApiKey).AddProject("my-project");
    /// var keycloakDb = neon.AddDatabase("keycloakdb");
    ///
    /// // Default public schema:
    /// var keycloak = builder.AddKeycloak("keycloak")
    ///     .WithNeonDatabase(keycloakDb);
    ///
    /// // Explicit schema:
    /// var keycloak = builder.AddKeycloak("keycloak")
    ///     .WithNeonDatabase(keycloakDb, schema: "keycloak");
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<KeycloakResource> WithNeonDatabase(
        this IResourceBuilder<KeycloakResource> builder,
        IResourceBuilder<NeonDatabaseResource> database,
        bool xaEnabled = false,
        string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        builder
            .WithEnvironment(ctx =>
            {
                NeonDatabaseResource db = database.Resource;

                ctx.EnvironmentVariables["KC_DB"] = "postgres";
                ctx.EnvironmentVariables["KC_DB_URL"] =
                    $"jdbc:postgresql://{db.Host}:{db.Port}/{db.DatabaseName}";
                ctx.EnvironmentVariables["KC_DB_USERNAME"] = db.RoleName ?? string.Empty;
                ctx.EnvironmentVariables["KC_DB_PASSWORD"] = db.Password ?? string.Empty;
                ctx.EnvironmentVariables["KC_TRANSACTION_XA_ENABLED"] =
                    xaEnabled.ToString().ToLowerInvariant();

                if (!string.IsNullOrWhiteSpace(schema))
                {
                    ctx.EnvironmentVariables["KC_DB_SCHEMA"] = schema;
                }
            })
            .WaitFor(database);

        return builder;
    }
}
