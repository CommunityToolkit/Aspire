using Aspire.Hosting.ApplicationModel;
using System.Text;
using System.Text.Json;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding SqlServer resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class SqlServerBuilderExtensions
{
    /// <summary>
    /// Adds an administration and development platform for SqlServer to the application model using DbGate.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="DbGateContainerImageTags.Tag"/> tag of the <inheritdoc cref="DbGateContainerImageTags.Image"/> container image.
    /// </remarks>
    /// <param name="builder">The SqlServer server resource builder.</param>
    /// <param name="configureContainer">Configuration callback for DbGate container resource.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <example>
    /// Use in application host with a SqlServer resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var sqlserver = builder.AddSqlServer("sqlserver")
    ///    .WithDbGate();
    /// var db = sqlserver.AddDatabase("db");
    ///
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SqlServerServerResource> WithDbGate(this IResourceBuilder<SqlServerServerResource> builder, Action<IResourceBuilder<DbGateContainerResource>>? configureContainer = null, string? containerName = null)
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
    /// Adds an administration and development platform for SqlServer to the application model using Adminer.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="AdminerContainerImageTags.Tag"/> tag of the <inheritdoc cref="AdminerContainerImageTags.Image"/> container image.
    /// <param name="builder">The SqlServer server resource builder.</param>
    /// <param name="configureContainer">Configuration callback for Adminer container resource.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <example>
    /// Use in application host with a SqlServer resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var sqlserver = builder.AddSqlServer("sqlserver")
    ///    .WithAdminer();
    /// var db = sqlserver.AddDatabase("db");
    ///
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SqlServerServerResource> WithAdminer(this IResourceBuilder<SqlServerServerResource> builder, Action<IResourceBuilder<AdminerContainerResource>>? configureContainer = null, string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        containerName ??= $"{builder.Resource.Name}-adminer";
        var adminerBuilder = AdminerBuilderExtensions.AddAdminer(builder.ApplicationBuilder, containerName);

        adminerBuilder
            .WithEnvironment(context => ConfigureAdminerContainer(context, builder.ApplicationBuilder));

        configureContainer?.Invoke(adminerBuilder);

        return builder;
    }

    private static void ConfigureDbGateContainer(EnvironmentCallbackContext context, IResourceBuilder<SqlServerServerResource> builder)
    {
        var sqlServerResource = builder.Resource;

        var name = sqlServerResource.Name;
        var label = $"LABEL_{name}";

        // Multiple WithDbGate calls will be ignored
        if (context.EnvironmentVariables.ContainsKey(label))
        {
            return;
        }

        // DbGate assumes SqlServer is being accessed over a default Aspire container network and hardcodes the resource address
        // This will need to be refactored once updated service discovery APIs are available
        context.EnvironmentVariables.Add(label, sqlServerResource.Name);
        context.EnvironmentVariables.Add($"SERVER_{name}", sqlServerResource.Name);
        context.EnvironmentVariables.Add($"USER_{name}", "sa");
        context.EnvironmentVariables.Add($"PASSWORD_{name}", sqlServerResource.PasswordParameter);
        context.EnvironmentVariables.Add($"PORT_{name}", sqlServerResource.PrimaryEndpoint.TargetPort!.ToString()!);
        context.EnvironmentVariables.Add($"ENGINE_{name}", "mssql@dbgate-plugin-mssql");

        if (context.EnvironmentVariables.GetValueOrDefault("CONNECTIONS") is string { Length: > 0 } connections)
        {
            context.EnvironmentVariables["CONNECTIONS"] = $"{connections},{name}";
        }
        else
        {
            context.EnvironmentVariables["CONNECTIONS"] = name;
        }
    }

    private static async Task ConfigureAdminerContainer(EnvironmentCallbackContext context, IDistributedApplicationBuilder applicationBuilder)
    {
        var sqlServerInstances = applicationBuilder.Resources.OfType<SqlServerServerResource>();

        string ADMINER_SERVERS = context.EnvironmentVariables.GetValueOrDefault("ADMINER_SERVERS")?.ToString() ?? string.Empty;

        var new_servers = sqlServerInstances.ToDictionary(
             sqlServerServerResource => sqlServerServerResource.Name,
             async sqlServerServerResource =>
             {
                 return new AdminerLoginServer
                 {
                     Server = sqlServerServerResource.Name,
                     UserName = "sa",
                     Password = await sqlServerServerResource.PasswordParameter.GetValueAsync(context.CancellationToken),
                     Driver = "mssql"
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
                servers!.Add(server.Key, await server.Value);
            }
        }
        string servers_json = JsonSerializer.Serialize(servers);
        context.EnvironmentVariables["ADMINER_SERVERS"] = servers_json;
    }
}