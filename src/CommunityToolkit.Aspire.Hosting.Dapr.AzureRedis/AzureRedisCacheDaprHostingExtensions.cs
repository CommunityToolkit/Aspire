using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Dapr;
using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.KeyVault;
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
    /// <param name="redisBuilder">The Azure Redis cache resource builder.</param>
    /// <returns>The updated Dapr component resource builder.</returns>
    private static IResourceBuilder<IDaprComponentResource> ConfigureRedisStateComponent(
        this IResourceBuilder<IDaprComponentResource> builder,
        IResourceBuilder<AzureRedisCacheResource> redisBuilder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(redisBuilder, nameof(redisBuilder));

        var daprComponent = AzureDaprHostingExtensions.CreateDaprComponent(redisDaprState, "state.redis", "v1.0");

        var redisHost = new ProvisioningParameter("redisHost", typeof(string));
        var principalIdParameter = new ProvisioningParameter(AzureBicepResource.KnownParameters.PrincipalId, typeof(string));

        var configureInfrastructure = AzureDaprHostingExtensions.GetInfrastructureConfigurationAction(daprComponent, [redisHost]);

        var daprResourceBuilder = builder.AddAzureDaprResource(redisDaprState, configureInfrastructure);


        redisBuilder.ConfigureInfrastructure(infra =>
        {
            var redisCacheResource = infra.GetProvisionableResources().OfType<AzureRedisResource>().Single();

            // Make necessary changes to the redis resource
            bool useEntraID = redisCacheResource.RedisConfiguration.IsAadEnabled.Value == "true";
            bool enableTLS = redisCacheResource.EnableNonSslPort.Value == false;

            BicepValue<int> port = enableTLS ? redisCacheResource.SslPort : redisCacheResource.Port;

            infra.Add(new ProvisioningOutput("daprConnectionString", typeof(string))
            {
                Value = BicepFunction.Interpolate($"{redisCacheResource.HostName}:{port}")
            });

            daprResourceBuilder.WithParameter("redisHost", redisBuilder.GetOutput("daprConnectionString"));

            daprComponent.Metadata = [
                new ContainerAppDaprMetadata { Name = "redisHost", Value = redisHost },
                new ContainerAppDaprMetadata { Name = "enableTLS", Value = enableTLS? "true":"false"},
                new ContainerAppDaprMetadata { Name = "actorStateStore", Value = "true" }
            ];


            if (useEntraID)
            {
                daprComponent.Metadata.Add(new ContainerAppDaprMetadata
                {
                    Name = "useEntraID",
                    Value = "true"
                });
                daprComponent.Metadata.Add(new ContainerAppDaprMetadata
                {
                    Name = "azureClientId",
                    Value = principalIdParameter
                });
            }
            else
            {
                infra.ConfigureSecretAccess(daprComponent, redisCacheResource);
                daprResourceBuilder.WithParameter("redisPasswordSecretUri", redisBuilder.GetOutput("redisPasswordSecretUri"));
            }
        });
        
        // return the original builder to allow chaining
        return builder;
    }

    /// <summary>
    /// Configures secrets access for the Azure Redis Cache and sets up the necessary Dapr component secrets.
    /// </summary>
    /// <param name="redisCache">The Azure Redis Cache resource infrastructure.</param>
    /// <param name="daprComponent">The Dapr component for the container app managed environment.</param>
    /// <param name="redisCacheResource">The Azure Redis resource containing the keys.</param>
    private static void ConfigureSecretAccess(this AzureResourceInfrastructure redisCache,
                                              ContainerAppManagedEnvironmentDaprComponent daprComponent,
                                              AzureRedisResource redisCacheResource)
    {
        ArgumentNullException.ThrowIfNull(redisCache, nameof(redisCache));
        ArgumentNullException.ThrowIfNull(daprComponent, nameof(daprComponent));
        ArgumentNullException.ThrowIfNull(redisCacheResource, nameof(redisCacheResource));
        
        var redisPasswordSecret = new ProvisioningParameter("redisPasswordSecretUri", typeof(Uri));

        var keyVault = redisCache.GetProvisionableResources()
                                                 .OfType<KeyVaultService>()
                                                 .FirstOrDefault() ?? redisCache.ConfigureKeyVaultSecrets();

        var redisPassword = new KeyVaultSecret("daprRedisPassword")
        {
            Parent = keyVault,
            Name = "daprRedisPassword",
            Properties = new SecretProperties
            {
                Value = redisCacheResource.GetKeys().PrimaryKey
            }
        };

        redisCache.Add(redisPassword);

        redisCache.Add(new ProvisioningOutput("redisPasswordSecretUri", typeof(Uri))
        {
            Value = redisPassword.Properties.SecretUri
        });

        daprComponent.Metadata.Add(new ContainerAppDaprMetadata
        {
            Name = "redisPassword",
            SecretRef = "redisPassword"
        });

        daprComponent.Secrets = [
            new ContainerAppWritableSecret {
                        Name = "redisPassword",
                        KeyVaultUri = redisPasswordSecret
                     }
        ];
    }
}
