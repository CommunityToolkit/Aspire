using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Dapr;
using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;
using AzureRedisResource = Azure.Provisioning.Redis.RedisResource;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring Dapr components with Azure Redis.
/// </summary>
public static class AzureRedisCacheDaprHostingExtensions
{
    private const string redisDaprState = nameof(redisDaprState);
    /// <summary>
    /// Configures a Dapr component resource to use an Azure Redis cache resource.
    /// </summary>
    /// <param name="builder">The Dapr component resource builder.</param>
    /// <param name="source">The Azure Redis cache resource builder.</param>
    /// <returns>The updated Dapr component resource builder.</returns>
    public static IResourceBuilder<IDaprComponentResource> WithReference(
        this IResourceBuilder<IDaprComponentResource> builder,
        IResourceBuilder<AzureRedisCacheResource> source) =>
        builder.ApplicationBuilder.ExecutionContext.IsRunMode ? builder : builder.Resource.Type switch
        {
            "state" => builder.ConfigureRedisStateComponent(source),
            _ => throw new InvalidOperationException($"Unsupported Dapr component type: {builder.Resource.Type}"),
        };

    /// <summary>
    /// Configures the Redis state component for the Dapr component resource.
    /// </summary>
    /// <param name="builder">The Dapr component resource builder.</param>
    /// <param name="source">The Azure Redis cache resource builder.</param>
    /// <returns>The updated Dapr component resource builder.</returns>
    private static IResourceBuilder<IDaprComponentResource> ConfigureRedisStateComponent(
        this IResourceBuilder<IDaprComponentResource> builder,
        IResourceBuilder<AzureRedisCacheResource> source)
    {
        var daprComponent = AzureDaprHostingExtensions.CreateDaprComponent(redisDaprState, "state.redis", "v1.0");

        source.ConfigureInfrastructure(redisCache =>
        {
            var redisCacheResource = redisCache.GetProvisionableResources().OfType<AzureRedisResource>().Single();

            // Make necessary changes to the redis resource
            bool useEntraID = redisCacheResource.RedisConfiguration.IsAadEnabled.Equals("true");
            bool enableTLS = redisCacheResource.EnableNonSslPort.Equals("false");

            BicepValue<int> port = enableTLS ? redisCacheResource.SslPort : redisCacheResource.Port;

            redisCache.Add(new ProvisioningOutput("DaprConnectionString", typeof(string))
            {
                Value = BicepFunction.Interpolate($"{redisCacheResource.HostName}:{port}")
            });

            var redisHost = new ProvisioningParameter("redisHost", typeof(string));

            ContainerAppDaprMetadata securityMetadata = useEntraID ?
                new ContainerAppDaprMetadata { Name = "useEntraID", Value = "true" } :
                new ContainerAppDaprMetadata { Name = "redisPassword", SecretRef = "redisPassword" };

            daprComponent.Metadata = [
                new ContainerAppDaprMetadata { Name = "redisHost", Value = redisHost },
                                            securityMetadata,
                                            new ContainerAppDaprMetadata { Name = "enableTLS", Value = enableTLS? "true":"false"},
                                            new ContainerAppDaprMetadata { Name = "actorStateStore", Value = "true" }
            ];

            if (!useEntraID)
            {
                // TODO: Add key vault details
                daprComponent.Secrets = [
                    new ContainerAppWritableSecret { Name = "redisPassword", Value="redacted" }
                ];
            }

        });

        var configureInfrastructure = AzureDaprHostingExtensions.ConfigureInfrastructure(daprComponent);

        return builder.AddAzureDaprResource(redisDaprState, configureInfrastructure);
    }
}
