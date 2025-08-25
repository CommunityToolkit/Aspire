using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.KeyVault;
using CommunityToolkit.Aspire.Hosting.Azure.Dapr;
using CommunityToolkit.Aspire.Hosting.Dapr;
using AzureRedisResource = Azure.Provisioning.Redis.RedisResource;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring Dapr components with Azure Redis.
/// </summary>
public static class AzureRedisCacheDaprHostingExtensions
{
    private const string secretStoreComponentKey = "secretStoreComponent";
    private const string redisKeyVaultNameKey = "redisKeyVaultName";
    private const string redisHostKey = "redisHost";
    private const string daprConnectionStringKey = "daprConnectionString";

    /// <summary>
    /// Configures a Dapr component resource to use an Azure Redis cache resource.
    /// </summary>
    /// <param name="builder">The Dapr component resource builder.</param>
    /// <param name="source">The Azure Redis cache resource builder.</param>
    /// <returns>The updated Dapr component resource builder.</returns>
    public static IResourceBuilder<IDaprComponentResource> WithReference(this IResourceBuilder<IDaprComponentResource> builder, IResourceBuilder<AzureRedisCacheResource> source)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
            return builder;

        return builder.Resource.Type switch
        {
            "state" => builder.ConfigureRedisStateComponent(source),
            "pubsub" => builder.ConfigureRedisPubSubComponent(source),
            _ => throw new InvalidOperationException($"Unsupported Dapr component type: {builder.Resource.Type}")
        };
    }

    // Private methods do not require XML documentation.

    private static IResourceBuilder<IDaprComponentResource> ConfigureRedisStateComponent(this IResourceBuilder<IDaprComponentResource> builder, IResourceBuilder<AzureRedisCacheResource> redisBuilder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(redisBuilder, nameof(redisBuilder));

        if (redisBuilder.Resource.UseAccessKeyAuthentication)
        {
            builder.ConfigureForAccessKeyAuthentication(redisBuilder, "state.redis");
        }
        else
        {
            builder.ConfigureForManagedIdentityAuthentication(redisBuilder, "state.redis");
        }

        return builder;
    }

    private static IResourceBuilder<IDaprComponentResource> ConfigureRedisPubSubComponent(this IResourceBuilder<IDaprComponentResource> builder, IResourceBuilder<AzureRedisCacheResource> redisBuilder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(redisBuilder, nameof(redisBuilder));

        if (redisBuilder.Resource.UseAccessKeyAuthentication)
        {
            builder.ConfigureForAccessKeyAuthentication(redisBuilder, "pubsub.redis");
        }
        else
        {
            builder.ConfigureForManagedIdentityAuthentication(redisBuilder, "pubsub.redis");
        }

        return builder;
    }

    private static void ConfigureForManagedIdentityAuthentication(this IResourceBuilder<IDaprComponentResource> builder, IResourceBuilder<AzureRedisCacheResource> redisBuilder, string componentType)
    {
        var redisHostParam = new ProvisioningParameter(redisHostKey, typeof(string));
        var principalIdParam = new ProvisioningParameter(AzureBicepResource.KnownParameters.PrincipalId, typeof(string));

        var configureInfrastructure = (AzureResourceInfrastructure infrastructure) =>
        {
            if (infrastructure.GetProvisionableResources().OfType<ContainerAppManagedEnvironment>().FirstOrDefault() is ContainerAppManagedEnvironment managedEnvironment)
            {
                var daprComponent = AzureDaprHostingExtensions.CreateDaprComponent(
                    builder.Resource.Name,
                    BicepFunction.Interpolate($"{builder.Resource.Name}"),
                    componentType,
                    "v1");

                daprComponent.Parent = managedEnvironment;
                
                var metadata = new List<ContainerAppDaprMetadata>
                {
                    new() { Name = redisHostKey, Value = redisHostParam },
                    new() { Name = "enableTLS", Value = "true" },
                    new() { Name = "useEntraID", Value = "true" },
                    new() { Name = "azureClientId", Value = principalIdParam }
                };

                // Add state-specific metadata
                if (componentType == "state.redis")
                {
                    metadata.Add(new ContainerAppDaprMetadata { Name = "actorStateStore", Value = "true" });
                }

                daprComponent.Metadata = [.. metadata];

                // Add scopes if any exist
                builder.AddScopes(daprComponent);

                infrastructure.Add(daprComponent);
                infrastructure.Add(redisHostParam);
                infrastructure.Add(principalIdParam);
            }
        };

        builder.WithAnnotation(new AzureDaprComponentPublishingAnnotation(configureInfrastructure));

        // Configure the Redis resource to output the connection string
        redisBuilder.ConfigureInfrastructure(infrastructure =>
        {
            var redisResource = infrastructure.GetProvisionableResources().OfType<AzureRedisResource>().SingleOrDefault();
            if (redisResource is not null)
            {
                infrastructure.Add(new ProvisioningOutput(daprConnectionStringKey, typeof(string))
                {
                    Value = BicepFunction.Interpolate($"{redisResource.HostName}:{redisResource.SslPort}")
                });
            }
        });
    }

    private static void ConfigureForAccessKeyAuthentication(this IResourceBuilder<IDaprComponentResource> builder, IResourceBuilder<AzureRedisCacheResource> redisBuilder, string componentType)
    {
        var redisHostParam = new ProvisioningParameter(redisHostKey, typeof(string));
        var kvNameParam = new ProvisioningParameter(redisKeyVaultNameKey, typeof(string));
        var secretStoreComponent = new ProvisioningParameter(secretStoreComponentKey, typeof(string));

        // Configure Key Vault secret store component - this adds the annotation to the same resource
        builder.ConfigureKeyVaultSecretsComponent(kvNameParam);

        var configureInfrastructure = (AzureResourceInfrastructure infrastructure) =>
        {
            if (infrastructure.GetProvisionableResources().OfType<ContainerAppManagedEnvironment>().FirstOrDefault() is ContainerAppManagedEnvironment managedEnvironment)
            {
                var daprComponent = AzureDaprHostingExtensions.CreateDaprComponent(
                    builder.Resource.Name,
                    BicepFunction.Interpolate($"{builder.Resource.Name}"),
                    componentType,
                    "v1");

                daprComponent.Parent = managedEnvironment;

                var metadata = new List<ContainerAppDaprMetadata>
                {
                    new() { Name = redisHostKey, Value = redisHostParam },
                    new() { Name = "enableTLS", Value = "true" },
                    new() { Name = "redisPassword", SecretRef = "redis-password" }
                };

                // Add state-specific metadata
                if (componentType == "state.redis")
                {
                    metadata.Add(new ContainerAppDaprMetadata { Name = "actorStateStore", Value = "true" });
                }

                daprComponent.Metadata = [.. metadata];
                daprComponent.SecretStoreComponent = secretStoreComponent;

                // Add scopes if any exist
                builder.AddScopes(daprComponent);

                infrastructure.Add(daprComponent);
                infrastructure.Add(redisHostParam);
                infrastructure.Add(secretStoreComponent);
            }
        };

        builder.WithAnnotation(new AzureDaprComponentPublishingAnnotation(configureInfrastructure));

        // Configure the Redis resource to output the connection string and set up Key Vault secret
        redisBuilder.ConfigureInfrastructure(infrastructure =>
        {
            var redisResource = infrastructure.GetProvisionableResources().OfType<AzureRedisResource>().SingleOrDefault();
            if (redisResource is not null)
            {
                var keyVault = infrastructure.GetProvisionableResources().OfType<KeyVaultService>().SingleOrDefault();
                if (keyVault is null)
                {
                    keyVault = KeyVaultService.FromExisting("keyVault");
                    infrastructure.Add(keyVault);
                }

                var secret = new KeyVaultSecret("redisPassword")
                {
                    Parent = keyVault,
                    Name = "redis-password",
                    Properties = new SecretProperties
                    {
                        Value = redisResource.GetKeys().PrimaryKey
                    }
                };

                infrastructure.Add(secret);

                infrastructure.Add(new ProvisioningOutput(redisKeyVaultNameKey, typeof(string))
                {
                    Value = keyVault.Name
                });

                infrastructure.Add(new ProvisioningOutput(daprConnectionStringKey, typeof(string))
                {
                    Value = BicepFunction.Interpolate($"{redisResource.HostName}:{redisResource.SslPort}")
                });
            }
        });
    }
}
