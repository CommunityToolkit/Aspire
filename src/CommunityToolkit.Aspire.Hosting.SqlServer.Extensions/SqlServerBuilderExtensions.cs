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

        containerName ??= $"{builder.Resource.Name}-dbgate";

        var dbGateBuilder = DbGateBuilderExtensions.AddDbGate(builder.ApplicationBuilder, containerName);

        dbGateBuilder
            .WithEnvironment(context => ConfigureDbGateContainer(context, builder.ApplicationBuilder));

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

    private static void ConfigureDbGateContainer(EnvironmentCallbackContext context, IDistributedApplicationBuilder applicationBuilder)
    {
        var sqlServerInstances = applicationBuilder.Resources.OfType<SqlServerServerResource>();

        var counter = 1;

        // Multiple WithDbGate calls will be ignored
        if (context.EnvironmentVariables.ContainsKey($"LABEL_sqlserver{counter}"))
        {
            return;
        }

        foreach (var sqlServerResource in sqlServerInstances)
        {
            // DbGate assumes SqlServer is being accessed over a default Aspire container network and hardcodes the resource address
            // This will need to be refactored once updated service discovery APIs are available
            context.EnvironmentVariables.Add($"LABEL_sqlserver{counter}", sqlServerResource.Name);
            context.EnvironmentVariables.Add($"SERVER_sqlserver{counter}", sqlServerResource.Name);
            context.EnvironmentVariables.Add($"USER_sqlserver{counter}", "sa");
            context.EnvironmentVariables.Add($"PASSWORD_sqlserver{counter}", sqlServerResource.PasswordParameter);
            context.EnvironmentVariables.Add($"PORT_sqlserver{counter}", sqlServerResource.PrimaryEndpoint.TargetPort!.ToString()!);
            context.EnvironmentVariables.Add($"ENGINE_sqlserver{counter}", "mssql@dbgate-plugin-mssql");

            counter++;
        }

        var instancesCount = sqlServerInstances.Count();
        if (instancesCount > 0)
        {
            var strBuilder = new StringBuilder();
            strBuilder.AppendJoin(',', Enumerable.Range(1, instancesCount).Select(i => $"sqlserver{i}"));
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
            string servers_json = JsonSerializer.Serialize(new_servers);
            context.EnvironmentVariables["ADMINER_SERVERS"] = servers_json;
        }
        else
        {
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

}