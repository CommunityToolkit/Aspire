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
    /// This overload is not available in polyglot app hosts. Use the overload without the configuration callback instead.
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
#pragma warning disable ASPIREATS001
    [AspireExportIgnore(Reason = "The configuration callback depends on DbGate container APIs that are not exported to polyglot app hosts. Use the overload without a configuration callback instead.")]
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

    /// <summary>
    /// Adds an administration and development platform for MongoDB to the application model using DbGate.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="DbGateContainerImageTags.Tag"/> tag of the <inheritdoc cref="DbGateContainerImageTags.Image"/> container image.
    /// </remarks>
    /// <param name="builder">The MongoDB server resource builder.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport("withDbGate")]
    internal static IResourceBuilder<MongoDBServerResource> WithDbGateForPolyglot(this IResourceBuilder<MongoDBServerResource> builder, string? containerName = null)
    {
        return builder.WithDbGate(configureContainer: null, containerName);
    }
#pragma warning restore ASPIREATS001
    
    
    /// <summary>
    /// Adds an administration and development platform for MongoDB to the application model using dbx.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="DbxContainerImageTags.Tag"/> tag of the <inheritdoc cref="DbxContainerImageTags.Image"/> container image.
    /// This overload is not available in polyglot app hosts. Use <see cref="WithDbx(IResourceBuilder{MongoDBServerResource}, string, string)"/> instead.
    /// </remarks>
    /// <param name="builder">The MongoDB server resource builder.</param>
    /// <param name="configureContainer">Configuration callback for dbx container resource.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <example>
    /// Use in application host with a MongoDB resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var mongodb = builder.AddMongoDB("mongodb")
    ///    .WithDbx();
    /// var db = mongodb.AddDatabase("db");
    ///
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExportIgnore(Reason = "Action<IResourceBuilder<DbxContainerResource>> is not supported reliably in polyglot app hosts. Use the container options overload instead.")]
    public static IResourceBuilder<MongoDBServerResource> WithDbx(this IResourceBuilder<MongoDBServerResource> builder, Action<IResourceBuilder<DbxContainerResource>>? configureContainer = null, string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        containerName ??= "dbx";
        var dbxBuilder = DbxBuilderExtensions.AddDbx(builder.ApplicationBuilder, containerName);

        dbxBuilder
            .WithEnvironment(context => ConfigureDbxContainer(context, dbxBuilder, builder));

        configureContainer?.Invoke(dbxBuilder);

        return builder;
    }

    /// <summary>
    /// Adds an administration and development platform for MongoDB to the application model using dbx.
    /// </summary>
    /// <param name="builder">The MongoDB server resource builder.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <param name="imageTag">Optional image tag override for the dbx container.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    internal static IResourceBuilder<MongoDBServerResource> WithDbx(this IResourceBuilder<MongoDBServerResource> builder, string? containerName = null, string? imageTag = null)
    {
        Action<IResourceBuilder<DbxContainerResource>>? configureContainer = null;
        if (!string.IsNullOrWhiteSpace(imageTag))
        {
            configureContainer = dbxBuilder => dbxBuilder.WithImageTag(imageTag);
        }

        return WithDbx(builder, configureContainer, containerName);
    }

    private static void ConfigureDbGateContainer(EnvironmentCallbackContext context, IResourceBuilder<MongoDBServerResource> builder)
    {
        var mongoDBServer = builder.Resource;

        var name = mongoDBServer.Name;
        var connectionId = DbGateBuilderExtensions.SanitizeConnectionId(name);
        var label = $"LABEL_{connectionId}";

        // Multiple WithDbGate calls will be ignored
        if (context.EnvironmentVariables.ContainsKey(label))
        {
            return;
        }

        // DbGate assumes MongoDB is being accessed over a default Aspire container network and hardcodes the resource address
        // This will need to be refactored once updated service discovery APIs are available
        context.EnvironmentVariables.Add(label, name);
        context.EnvironmentVariables.Add($"URL_{connectionId}", mongoDBServer.ConnectionStringExpression);
        context.EnvironmentVariables.Add($"ENGINE_{connectionId}", "mongo@dbgate-plugin-mongo");

        if (context.EnvironmentVariables.GetValueOrDefault("CONNECTIONS") is string { Length: > 0 } connections)
        {
            context.EnvironmentVariables["CONNECTIONS"] = $"{connections},{connectionId}";
        }
        else
        {
            context.EnvironmentVariables["CONNECTIONS"] = connectionId;
        }
    }

    private static async Task ConfigureDbxContainer(
        EnvironmentCallbackContext context,
        IResourceBuilder<DbxContainerResource> dbxBuilder, 
        IResourceBuilder<MongoDBServerResource> builder
    )
    {
        var mongoDbServerResource = builder.Resource;
        
        dbxBuilder.Resource.AddConnection(
            new DbxConnectionConfig
            {
                Id = mongoDbServerResource.Name,
                Name = mongoDbServerResource.Name,
                DbType = DbxDatabaseType.MongoDb,
                Host = mongoDbServerResource.Name,
                Port = ushort.Parse(mongoDbServerResource.PrimaryEndpoint.TargetPort!.Value.ToString()),
                Username = await mongoDbServerResource.UserNameReference.GetValueAsync(context.CancellationToken) ?? string.Empty,
                Password = mongoDbServerResource.PasswordParameter is not null 
                    ? await mongoDbServerResource.PasswordParameter.GetValueAsync(context.CancellationToken) ?? string.Empty 
                    : string.Empty,
            }    
        );
    }
}
