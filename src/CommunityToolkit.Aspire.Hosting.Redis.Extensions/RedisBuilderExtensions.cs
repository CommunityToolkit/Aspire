using Aspire.Hosting.ApplicationModel;
using System.Text;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Redis resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class RedisBuilderExtensions
{
    /// <summary>
    /// Adds an administration and development platform for Redis to the application model using DbGate.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="DbGateContainerImageTags.Tag"/> tag of the <inheritdoc cref="DbGateContainerImageTags.Image"/> container image.
    /// </remarks>
    /// <param name="builder">The Redis server resource builder.</param>
    /// <param name="configureContainer">Configuration callback for DbGate container resource.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <example>
    /// Use in application host with a Redis resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var redis = builder.AddRedis("redis")
    ///    .WithDbGate();
    ///
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(redis);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<RedisResource> WithDbGate(this IResourceBuilder<RedisResource> builder, Action<IResourceBuilder<DbGateContainerResource>>? configureContainer = null, string? containerName = null)
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

    private static void ConfigureDbGateContainer(EnvironmentCallbackContext context, IResourceBuilder<RedisResource> builder)
    {
        var redisResource = builder.Resource;

        var name = redisResource.Name;
        var connectionId = DbGateBuilderExtensions.SanitizeConnectionId(name);
        var label = $"LABEL_{connectionId}";

        // DbGate assumes Redis is being accessed over a default Aspire container network and hardcodes the resource address
        var redisUrl = redisResource.PasswordParameter is not null ?
            ReferenceExpression.Create($"redis://:{redisResource.PasswordParameter}@{name}:{redisResource.PrimaryEndpoint.TargetPort?.ToString()}") :
            ReferenceExpression.Create($"redis://{name}:{redisResource.PrimaryEndpoint.TargetPort?.ToString()}");

        context.EnvironmentVariables.Add(label, name);
        context.EnvironmentVariables.Add($"URL_{connectionId}", redisUrl);
        context.EnvironmentVariables.Add($"ENGINE_{connectionId}", "redis@dbgate-plugin-redis");

        if (context.EnvironmentVariables.GetValueOrDefault("CONNECTIONS") is string { Length: > 0 } connections)
        {
            context.EnvironmentVariables["CONNECTIONS"] = $"{connections},{connectionId}";
        }
        else
        {
            context.EnvironmentVariables["CONNECTIONS"] = connectionId;
        }
    }
}