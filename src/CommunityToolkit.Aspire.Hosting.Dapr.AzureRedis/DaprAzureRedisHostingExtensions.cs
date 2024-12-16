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
public static class DaprAzureRedisHostingExtensions
{
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
    /// Configures a Dapr component resource to use an Azure Redis cache resource for state management.
    /// </summary>
    /// <param name="builder">The Dapr component resource builder.</param>
    /// <param name="redisCache">The Azure Redis cache resource builder.</param>
    /// <returns>The updated Dapr component resource builder.</returns>
    private static IResourceBuilder<IDaprComponentResource> ConfigureRedisStateComponent(
        this IResourceBuilder<IDaprComponentResource> builder,
        IResourceBuilder<AzureRedisCacheResource> redisCache)
    {
        builder.ApplicationBuilder.AddAzureRedis("");
        return builder.ConfigureForAzure(redisCache, "state.redis", "v1.0",
                        (module, daprComponent) =>
                        {
                            var redisCacheResource = module.GetProvisionableResources().OfType<AzureRedisResource>().Single();

                            // Make necessary changes to the redis resource

                            bool useEntraID = redisCacheResource.RedisConfiguration.IsAadEnabled.Equals("true");
                            bool enableTLS = redisCacheResource.EnableNonSslPort.Equals("false");

                            BicepValue<int> port = enableTLS ? redisCacheResource.SslPort : redisCacheResource.Port;

                            module.Add(new ProvisioningOutput("DaprConnectionString", typeof(string))
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
                                daprComponent.Secrets = [
                                    new ContainerAppWritableSecret { Name = "redisPassword", KeyVaultUri = new Uri("") }
                                ];
                            }
                            return [redisHost];
                        });
    }
}
