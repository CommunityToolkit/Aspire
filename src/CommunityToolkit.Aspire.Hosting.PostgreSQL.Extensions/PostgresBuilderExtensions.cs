using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.DbGate;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding PostgreSQL resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class PostgresBuilderExtensions
{
    /// <summary>
    /// Adds an administration and development platform for PostgreSQL to the application model using DbGate.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="DbGateContainerImageTags.Tag"/> tag of the <inheritdoc cref="DbGateContainerImageTags.Image"/> container image.
    /// </remarks>
    /// <param name="builder">The Postgres server resource builder.</param>
    /// <param name="configureContainer">Configuration callback for DbGate container resource.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <example>
    /// Use in application host with a Postgres resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var postgres = builder.AddPostgres("postgres")
    ///    .WithDbGate();
    /// var db = postgres.AddDatabase("db");
    ///
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<PostgresServerResource> WithDbGate(this IResourceBuilder<PostgresServerResource> builder, Action<IResourceBuilder<DbGateContainerResource>>? configureContainer = null, string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        containerName ??= $"{builder.Resource.Name}-dbgate";

        var dbGateBuilder = DbGateBuilderExtensions.AddDbGate(builder.ApplicationBuilder, containerName);

        dbGateBuilder
            .WithEnvironment(context => ConfigureDbGateContainer(context, builder.ApplicationBuilder))
            .WaitFor(builder);

        configureContainer?.Invoke(dbGateBuilder);

        return builder;
    }


    /// <summary>
    /// Configures the host port that the DbGate resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder for DbGate.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The resource builder for DbGate.</returns>
    public static IResourceBuilder<DbGateContainerResource> WithHostPort(this IResourceBuilder<DbGateContainerResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return DbGateBuilderExtensions.WithHostPort(builder, port);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a DbGate container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DbGateContainerResource> WithDataVolume(this IResourceBuilder<DbGateContainerResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return DbGateBuilderExtensions.WithDataVolume(builder, name, isReadOnly);
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a DbGate container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DbGateContainerResource> WithDataBindMount(this IResourceBuilder<DbGateContainerResource> builder, string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return DbGateBuilderExtensions.WithDataBindMount(builder, source, isReadOnly);
    }


    private static void ConfigureDbGateContainer(EnvironmentCallbackContext context, IDistributedApplicationBuilder applicationBuilder)
    {
        var postgresInstances = applicationBuilder.Resources.OfType<PostgresServerResource>();

        var counter = 1;
        foreach (var postgresServer in postgresInstances)
        {
            // Multiple WithDbGate calls will be ignored
            if (context.EnvironmentVariables.ContainsKey($"LABEL_postgres{counter}"))
            {
                continue;
            }

            var user = postgresServer.UserNameParameter?.Value ?? "postgres";

            // DbGate assumes Postgres is being accessed over a default Aspire container network and hardcodes the resource address
            // This will need to be refactored once updated service discovery APIs are available
            context.EnvironmentVariables.Add($"LABEL_postgres{counter}", postgresServer.Name);
            context.EnvironmentVariables.Add($"SERVER_postgres{counter}", postgresServer.Name);
            context.EnvironmentVariables.Add($"USER_postgres{counter}", user);
            context.EnvironmentVariables.Add($"PASSWORD_postgres{counter}", postgresServer.PasswordParameter.Value);
            context.EnvironmentVariables.Add($"PORT_postgres{counter}", postgresServer.PrimaryEndpoint.TargetPort!.ToString()!);
            context.EnvironmentVariables.Add($"ENGINE_postgres{counter}", "postgres@dbgate-plugin-postgres");

            string CONNECTIONS = context.EnvironmentVariables.GetValueOrDefault("CONNECTIONS")?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(CONNECTIONS))
            {
                context.EnvironmentVariables["CONNECTIONS"] = $"postgres{counter},";
            }
            else
            {
                context.EnvironmentVariables["CONNECTIONS"] += $"postgres{counter},";
            }

            counter++;
        }

        if (context.EnvironmentVariables.TryGetValue("CONNECTIONS", out object? value) && value is not null)
        {
            string CONNECTIONS = value.ToString()!;
            if (CONNECTIONS.EndsWith(','))
            {
                context.EnvironmentVariables["CONNECTIONS"] = CONNECTIONS.Remove(CONNECTIONS.Length - 1, 1);
            }
        }
    }
}