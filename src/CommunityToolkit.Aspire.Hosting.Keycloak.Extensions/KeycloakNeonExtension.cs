using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for integrating Keycloak resources with Neon Postgres databases.
/// </summary>
public static class KeycloakNeonExtension
{
    /// <summary>
    /// The container mount path where Neon provisioner output is made available.
    /// </summary>
    private const string NeonOutputMountPath = "/neon-output";

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
    /// Connection details are provided by the Neon provisioner via a shared volume.
    /// The provisioner writes per-database <c>.env</c> files containing <c>NEON_HOST</c>,
    /// <c>NEON_PORT</c>, <c>NEON_DATABASE</c>, <c>NEON_USERNAME</c>, and <c>NEON_PASSWORD</c>.
    /// The Keycloak entrypoint is replaced with a shell wrapper that sources this file,
    /// maps the values to the Keycloak-specific environment variables, and then starts Keycloak
    /// with whatever command-line arguments were previously configured.
    /// </para>
    /// <para>
    /// This mechanism differs slightly between run and publish modes:
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Run mode:</strong> the provisioner host process writes env files to
    /// a temp directory, which is bind-mounted into the Keycloak container.</description></item>
    /// <item><description><strong>Publish mode:</strong> the provisioner container writes env files to a
    /// named Docker volume, which is also mounted into the Keycloak container.</description></item>
    /// </list>
    /// <para>
    /// This method sets the following environment variables on the Keycloak container:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>KC_DB</c> — set to <c>postgres</c></description></item>
    /// <item><description><c>KC_DB_URL</c> — JDBC URL built from the Neon endpoint host, port, and database name (sourced at container startup)</description></item>
    /// <item><description><c>KC_DB_USERNAME</c> — the Neon database role name (sourced at container startup)</description></item>
    /// <item><description><c>KC_DB_PASSWORD</c> — the Neon database password (sourced at container startup)</description></item>
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

        NeonProjectResource neonProject = database.Resource.Parent;

        builder
            .WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_TRANSACTION_XA_ENABLED", xaEnabled.ToString().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(schema))
        {
            builder.WithEnvironment("KC_DB_SCHEMA", schema);
        }

        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            if (neonProject.HostOutputDirectory is not null)
            {
                builder.WithBindMount(neonProject.HostOutputDirectory, NeonOutputMountPath, isReadOnly: true);
            }
        }
        else
        {
            if (neonProject.OutputVolumeName is not null)
            {
                builder.WithAnnotation(new ContainerMountAnnotation(
                    neonProject.OutputVolumeName,
                    NeonOutputMountPath,
                    ContainerMountType.Volume,
                    isReadOnly: true));
            }
        }

        if (!builder.ApplicationBuilder.ExecutionContext.IsRunMode
            && neonProject.ProvisionerResource is ProjectResource provisionerProject)
        {
            builder.WaitForCompletion(
                builder.ApplicationBuilder.CreateResourceBuilder(provisionerProject));
        }

        var envFilePath = $"{NeonOutputMountPath}/{database.Resource.Name}.env";

        var startupScript =
            // Safety loop: wait until the provisioner has written the env file.
            $"while [ ! -f {envFilePath} ]; do echo 'Waiting for Neon provisioner output...'; sleep 2; done; " +
            // Source the env file (sets NEON_HOST, NEON_PORT, NEON_DATABASE, NEON_USERNAME, NEON_PASSWORD).
            $". {envFilePath}; " +
            // Map to Keycloak variables.
            "export KC_DB_URL=\"jdbc:postgresql://${NEON_HOST}:${NEON_PORT}/${NEON_DATABASE}\"; " +
            "export KC_DB_USERNAME=\"${NEON_USERNAME}\"; " +
            "export KC_DB_PASSWORD=\"${NEON_PASSWORD}\"; " +
            // Start Keycloak with the original positional arguments ($@ from sh -c).
            "exec /opt/keycloak/bin/kc.sh \"$@\"";

        builder.WithEntrypoint("/bin/sh");
        builder.WithArgs(ctx =>
        {
            var existingArgs = ctx.Args.ToList();
            ctx.Args.Clear();
            ctx.Args.Add("-c");
            ctx.Args.Add(startupScript);
            ctx.Args.Add("_"); // placeholder for $0 in sh -c
            foreach (var arg in existingArgs)
            {
                ctx.Args.Add(arg);
            }
        });

        builder.WaitFor(database);

        return builder;
    }
}
