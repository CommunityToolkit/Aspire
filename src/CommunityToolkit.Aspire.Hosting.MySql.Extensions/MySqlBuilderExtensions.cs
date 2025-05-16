using Aspire.Hosting.ApplicationModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding MySql resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class MySqlBuilderExtensions
{
    /// <summary>
    /// Adds an administration and development platform for MySql to the application model using Adminer.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="AdminerContainerImageTags.Tag"/> tag of the <inheritdoc cref="AdminerContainerImageTags.Image"/> container image.
    /// <param name="builder">The MySql server resource builder.</param>
    /// <param name="configureContainer">Configuration callback for Adminer container resource.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <example>
    /// Use in application host with a MySql resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var mysql = builder.AddMySql("mysql")
    ///    .WithAdminer();
    /// var db = mysql.AddDatabase("db");
    ///
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<MySqlServerResource> WithAdminer(this IResourceBuilder<MySqlServerResource> builder, Action<IResourceBuilder<AdminerContainerResource>>? configureContainer = null, string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        containerName ??= $"{builder.Resource.Name}-adminer";
        var adminerBuilder = AdminerBuilderExtensions.AddAdminer(builder.ApplicationBuilder, containerName);

        adminerBuilder
            .WithEnvironment(context => ConfigureAdminerContainer(context, builder.ApplicationBuilder));

        configureContainer?.Invoke(adminerBuilder);

        return builder;
    }

    internal static void ConfigureAdminerContainer(EnvironmentCallbackContext context, IDistributedApplicationBuilder applicationBuilder)
    {
        var mysqlInstances = applicationBuilder.Resources.OfType<MySqlServerResource>();

        string ADMINER_SERVERS = context.EnvironmentVariables.GetValueOrDefault("ADMINER_SERVERS")?.ToString() ?? string.Empty;

        var new_servers = mysqlInstances.ToDictionary(
             mysqlServer => mysqlServer.Name,
             mysqlServer =>
             {
                 return new AdminerLoginServer
                 {
                     Server = mysqlServer.Name,
                     UserName = "root",
                     Password = mysqlServer.PasswordParameter.Value,
                     Driver = "server" // driver for MySQL is called 'server'
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
                    servers!.Add(server.Key, server.Value);
                }
            }
            string servers_json = JsonSerializer.Serialize(servers);
            context.EnvironmentVariables["ADMINER_SERVERS"] = servers_json;
        }

    }
}