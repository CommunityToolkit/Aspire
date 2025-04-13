using Aspire.Hosting.ApplicationModel;
using System.Text;

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
            .WithEnvironment(context => ConfigureDbGateContainer(context, builder.ApplicationBuilder));
            

        configureContainer?.Invoke(dbGateBuilder);

        return builder;
    }

    private static void ConfigureDbGateContainer(EnvironmentCallbackContext context, IDistributedApplicationBuilder applicationBuilder)
    {
        var postgresInstances = applicationBuilder.Resources.OfType<PostgresServerResource>();

        var counter = 1;

        // Multiple WithDbGate calls will be ignored
        if (context.EnvironmentVariables.ContainsKey($"LABEL_postgres{counter}"))
        {
            return;
        }

        foreach (var postgresServer in postgresInstances)
        {
            var user = postgresServer.UserNameParameter?.Value ?? "postgres";

            // DbGate assumes Postgres is being accessed over a default Aspire container network and hardcodes the resource address
            // This will need to be refactored once updated service discovery APIs are available
            context.EnvironmentVariables.Add($"LABEL_postgres{counter}", postgresServer.Name);
            context.EnvironmentVariables.Add($"SERVER_postgres{counter}", postgresServer.Name);
            context.EnvironmentVariables.Add($"USER_postgres{counter}", user);
            context.EnvironmentVariables.Add($"PASSWORD_postgres{counter}", postgresServer.PasswordParameter.Value);
            context.EnvironmentVariables.Add($"PORT_postgres{counter}", postgresServer.PrimaryEndpoint.TargetPort!.ToString()!);
            context.EnvironmentVariables.Add($"ENGINE_postgres{counter}", "postgres@dbgate-plugin-postgres");

            counter++;
        }

        var instancesCount = postgresInstances.Count();
        if (instancesCount > 0)
        {
            var strBuilder = new StringBuilder();
            strBuilder.AppendJoin(',', Enumerable.Range(1, instancesCount).Select(i => $"postgres{i}"));
            var connections = strBuilder.ToString();

            string CONNECTIONS = context.EnvironmentVariables.GetValueOrDefault("CONNECTIONS")?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(CONNECTIONS))
            {
                context.EnvironmentVariables["CONNECTIONS"] = connections;
            }
            else
            {
                context.EnvironmentVariables["CONNECTIONS"] += $",{connections}";
            }
        }
    }
}