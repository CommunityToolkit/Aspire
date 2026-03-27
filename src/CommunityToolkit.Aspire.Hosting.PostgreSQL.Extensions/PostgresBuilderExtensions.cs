using Aspire.Hosting.ApplicationModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        containerName ??= "dbgate";

        var dbGateBuilder = DbGateBuilderExtensions.AddDbGate(builder.ApplicationBuilder, containerName);

        dbGateBuilder
            .WithEnvironment(context => ConfigureDbGateContainer(context, builder))
            .WaitFor(builder);

        configureContainer?.Invoke(dbGateBuilder);

        return builder;
    }

    /// <summary>
    /// Adds an administration and development platform for PostgreSQL to the application model using Adminer.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="AdminerContainerImageTags.Tag"/> tag of the <inheritdoc cref="AdminerContainerImageTags.Image"/> container image.
    /// <param name="builder">The Postgres server resource builder.</param>
    /// <param name="configureContainer">Configuration callback for Adminer container resource.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <example>
    /// Use in application host with a Postgres resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var postgres = builder.AddPostgres("postgres")
    ///    .WithAdminer();
    /// var db = postgres.AddDatabase("db");
    ///
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<PostgresServerResource> WithAdminer(this IResourceBuilder<PostgresServerResource> builder, Action<IResourceBuilder<AdminerContainerResource>>? configureContainer = null, string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        containerName ??= $"{builder.Resource.Name}-adminer";
        var adminerBuilder = AdminerBuilderExtensions.AddAdminer(builder.ApplicationBuilder, containerName);

        adminerBuilder
            .WithEnvironment(context => ConfigureAdminerContainer(context, builder.ApplicationBuilder));

        configureContainer?.Invoke(adminerBuilder);

        return builder;
    }

    private static void ConfigureDbGateContainer(EnvironmentCallbackContext context, IResourceBuilder<PostgresServerResource> builder)
    {
        var postgresServer = builder.Resource;

        var name = postgresServer.Name;
        var connectionId = DbGateBuilderExtensions.SanitizeConnectionId(name);
        var label = $"LABEL_{connectionId}";

        // Multiple WithDbGate calls will be ignored
        if (context.EnvironmentVariables.ContainsKey(label))
        {
            return;
        }

        var userParameter = postgresServer.UserNameParameter is null
         ? ReferenceExpression.Create($"postgres")
         : ReferenceExpression.Create($"{postgresServer.UserNameParameter}");

        // DbGate assumes Postgres is being accessed over a default Aspire container network and hardcodes the resource address
        // This will need to be refactored once updated service discovery APIs are available
        context.EnvironmentVariables.Add($"LABEL_{connectionId}", postgresServer.Name);
        context.EnvironmentVariables.Add($"SERVER_{connectionId}", postgresServer.Name);
        context.EnvironmentVariables.Add($"USER_{connectionId}", userParameter);
        context.EnvironmentVariables.Add($"PASSWORD_{connectionId}", postgresServer.PasswordParameter);
        context.EnvironmentVariables.Add($"PORT_{connectionId}", postgresServer.PrimaryEndpoint.TargetPort!.ToString()!);
        context.EnvironmentVariables.Add($"ENGINE_{connectionId}", "postgres@dbgate-plugin-postgres");

        if (context.EnvironmentVariables.GetValueOrDefault("CONNECTIONS") is string { Length: > 0 } connections)
        {
            context.EnvironmentVariables["CONNECTIONS"] = $"{connections},{connectionId}";
        }
        else
        {
            context.EnvironmentVariables["CONNECTIONS"] = connectionId;
        }
    }


    internal static async Task ConfigureAdminerContainer(EnvironmentCallbackContext context, IDistributedApplicationBuilder applicationBuilder)
    {
        var postgresInstances = applicationBuilder.Resources.OfType<PostgresServerResource>();

        string ADMINER_SERVERS = context.EnvironmentVariables.GetValueOrDefault("ADMINER_SERVERS")?.ToString() ?? string.Empty;

        var new_servers = postgresInstances.ToDictionary(
             postgresServer => postgresServer.Name,
             async postgresServer =>
             {
                 var user = postgresServer.UserNameParameter switch
                 {
                     null => "postgres",
                     _ => await postgresServer.UserNameParameter.GetValueAsync(context.CancellationToken)
                 };
                 return new AdminerLoginServer
                 {
                     Server = postgresServer.Name,
                     UserName = user,
                     Password = await postgresServer.PasswordParameter.GetValueAsync(context.CancellationToken),
                     Driver = "pgsql"
                 };
             });

        if (string.IsNullOrEmpty(ADMINER_SERVERS))
        {
            ADMINER_SERVERS = "{}"; // Initialize with an empty JSON object if not set
        }

        var servers = JsonSerializer.Deserialize<Dictionary<string, AdminerLoginServer>>(ADMINER_SERVERS) ?? throw new InvalidOperationException("The servers should not be null. This should never happen.");
        foreach (var server in new_servers)
        {
            if (!servers.ContainsKey(server.Key))
            {
                servers.Add(server.Key, await server.Value);
            }
        }
        string servers_json = JsonSerializer.Serialize(servers);
        context.EnvironmentVariables["ADMINER_SERVERS"] = servers_json;

    }
}