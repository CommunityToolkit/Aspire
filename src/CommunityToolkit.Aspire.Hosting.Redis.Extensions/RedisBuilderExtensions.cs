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

        containerName ??= $"{builder.Resource.Name}-dbgate";

        var dbGateBuilder = DbGateBuilderExtensions.AddDbGate(builder.ApplicationBuilder, containerName);

        dbGateBuilder
            .WithEnvironment(context => ConfigureDbGateContainer(context, builder.ApplicationBuilder));

        configureContainer?.Invoke(dbGateBuilder);

        return builder;
    }

    private static void ConfigureDbGateContainer(EnvironmentCallbackContext context, IDistributedApplicationBuilder applicationBuilder)
    {
        var reidsInstances = applicationBuilder.Resources.OfType<RedisResource>();

        var counter = 1;

        // Multiple WithDbGate calls will be ignored
        if (context.EnvironmentVariables.ContainsKey($"LABEL_redis{counter}"))
        {
            return;
        }

        foreach (var redisResource in reidsInstances)
        {

            // DbGate assumes Redis is being accessed over a default Aspire container network and hardcodes the resource address
            var redisUrl = redisResource.PasswordParameter is not null ?
                $"redis://:{redisResource.PasswordParameter.Value}@{redisResource.Name}:{redisResource.PrimaryEndpoint.TargetPort}" : $"redis://{redisResource.Name}:{redisResource.PrimaryEndpoint.TargetPort}";

            context.EnvironmentVariables.Add($"LABEL_redis{counter}", redisResource.Name);
            context.EnvironmentVariables.Add($"URL_redis{counter}", redisUrl);
            context.EnvironmentVariables.Add($"ENGINE_redis{counter}", "redis@dbgate-plugin-redis");

            counter++;
        }

        var instancesCount = reidsInstances.Count();
        if (instancesCount > 0)
        {
            var strBuilder = new StringBuilder();
            strBuilder.AppendJoin(',', Enumerable.Range(1, instancesCount).Select(i => $"redis{i}"));
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