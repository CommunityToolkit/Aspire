using Aspire.Hosting.ApplicationModel;
using System.Text;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding MongoDB resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class MongoDBBuilderExtensions
{
    /// <summary>
    /// Adds an administration and development platform for MongoDB to the application model using DbGate.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="DbGateContainerImageTags.Tag"/> tag of the <inheritdoc cref="DbGateContainerImageTags.Image"/> container image.
    /// </remarks>
    /// <param name="builder">The MongoDB server resource builder.</param>
    /// <param name="configureContainer">Configuration callback for DbGate container resource.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <example>
    /// Use in application host with a MongoDB resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var mongodb = builder.AddMongoDB("mongodb")
    ///    .WithDbGate();
    /// var db = mongodb.AddDatabase("db");
    ///
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<MongoDBServerResource> WithDbGate(this IResourceBuilder<MongoDBServerResource> builder, Action<IResourceBuilder<DbGateContainerResource>>? configureContainer = null, string? containerName = null)
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
        var mongoDBInstances = applicationBuilder.Resources.OfType<MongoDBServerResource>();

        var counter = 1;

        // Multiple WithDbGate calls will be ignored
        if (context.EnvironmentVariables.ContainsKey($"LABEL_mongodb{counter}"))
        {
            return;
        }

        foreach (var mongoDBServer in mongoDBInstances)
        {
            // DbGate assumes MongoDB is being accessed over a default Aspire container network and hardcodes the resource address
            // This will need to be refactored once updated service discovery APIs are available
            context.EnvironmentVariables.Add($"LABEL_mongodb{counter}", mongoDBServer.Name);
            context.EnvironmentVariables.Add($"URL_mongodb{counter}", mongoDBServer.ConnectionStringExpression);
            context.EnvironmentVariables.Add($"ENGINE_mongodb{counter}", "mongo@dbgate-plugin-mongo");

            counter++;
        }

        var instancesCount = mongoDBInstances.Count();
        if (instancesCount > 0)
        {
            var strBuilder = new StringBuilder();
            strBuilder.AppendJoin(',', Enumerable.Range(1, instancesCount).Select(i => $"mongodb{i}"));
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