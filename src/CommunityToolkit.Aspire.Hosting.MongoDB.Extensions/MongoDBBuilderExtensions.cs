using Aspire.Hosting.ApplicationModel;
using System.Diagnostics.Metrics;
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

        containerName ??= "dbgate";

        var dbGateBuilder = builder.ApplicationBuilder.AddDbGate(containerName);

        dbGateBuilder
            .WithEnvironment(context => ConfigureDbGateContainer(context, builder))
            .WaitFor(builder);

        configureContainer?.Invoke(dbGateBuilder);

        return builder;
    }

    private static void ConfigureDbGateContainer(EnvironmentCallbackContext context, IResourceBuilder<MongoDBServerResource> builder)
    {
        var mongoDBServer = builder.Resource;

        var name = mongoDBServer.Name;
        var label = $"LABEL_{name}";

        // Multiple WithDbGate calls will be ignored
        if (context.EnvironmentVariables.ContainsKey(label))
        {
            return;
        }

        // DbGate assumes MongoDB is being accessed over a default Aspire container network and hardcodes the resource address
        // This will need to be refactored once updated service discovery APIs are available
        context.EnvironmentVariables.Add(label, name);
        context.EnvironmentVariables.Add($"URL_{name}", mongoDBServer.ConnectionStringExpression);
        context.EnvironmentVariables.Add($"ENGINE_{name}", "mongo@dbgate-plugin-mongo");

        if (context.EnvironmentVariables.GetValueOrDefault("CONNECTIONS") is string { Length: > 0 } connections)
        {
            context.EnvironmentVariables["CONNECTIONS"] = $"{connections},{name}";
        }
        else
        {
            context.EnvironmentVariables["CONNECTIONS"] = name;
        }
    }
}