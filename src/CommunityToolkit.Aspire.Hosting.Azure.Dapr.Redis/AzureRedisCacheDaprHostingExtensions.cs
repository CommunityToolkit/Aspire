using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.KeyVault;
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
    private const string redisDaprComponent = nameof(redisDaprComponent);

    /// <summary>
    /// Configures a Dapr component resource to use an Azure Redis cache resource.
    /// </summary>
    /// <param name="builder">The Dapr component resource builder.</param>
    /// <param name="source">The Azure Redis cache resource builder.</param>
    /// <returns>The updated Dapr component resource builder.</returns>
    public static IResourceBuilder<IDaprComponentResource> WithReference(
        this IResourceBuilder<IDaprComponentResource> builder,
        IResourceBuilder<AzureRedisCacheResource> source)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
            return builder;

        return builder.Resource.Type switch
        {
            "state" => builder.ConfigureRedisStateComponent(source),
            _ => throw new InvalidOperationException($"Unsupported Dapr component type: {builder.Resource.Type}")
        };
    }

    // Private methods do not require XML documentation.

    private static IResourceBuilder<IDaprComponentResource> ConfigureRedisStateComponent(
        this IResourceBuilder<IDaprComponentResource> builder,
        IResourceBuilder<AzureRedisCacheResource> redisBuilder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(redisBuilder, nameof(redisBuilder));

        if (redisBuilder.Resource.UseAccessKeyAuthentication)
        {
            builder.ConfigureForAccessKeyAuthentication(redisBuilder);
        }
        else
        {
            builder.ConfigureForManagedIdentityAuthentication(redisBuilder);
        }

        return builder;
    }

    private static void ConfigureForManagedIdentityAuthentication(
        this IResourceBuilder<IDaprComponentResource> builder, IResourceBuilder<AzureRedisCacheResource> redisBuilder)
    {
        var redisHostParam = new ProvisioningParameter(redisHostKey, typeof(string));
        var principalIdParam = new ProvisioningParameter(AzureBicepResource.KnownParameters.PrincipalId, typeof(string));

        var daprResourceBuilder = builder.CreateDaprResourceBuilder([redisHostParam, principalIdParam], out var daprComponent)
                                         .WithParameter(redisHostKey, redisBuilder.GetOutput(daprConnectionStringKey));

        redisBuilder.ConfigureComponentMetadata(daprComponent, redisHostParam, [
            new ContainerAppDaprMetadata { Name = "useEntraID", Value = "true" },
            new ContainerAppDaprMetadata { Name = "azureClientId", Value = principalIdParam }
        ]);
    }

    private static void ConfigureForAccessKeyAuthentication(
        this IResourceBuilder<IDaprComponentResource> builder, IResourceBuilder<AzureRedisCacheResource> redisBuilder)
    {
        // Provisioning Params
        var redisHostParam = new ProvisioningParameter(redisHostKey, typeof(string));
        var kvNameParam = new ProvisioningParameter(redisKeyVaultNameKey, typeof(string));
        var secretStoreComponent = new ProvisioningParameter(secretStoreComponentKey, typeof(string));

        // create secret store component
        var secretStoreBuilder = builder.ConfigureKeyVaultSecretsComponent(kvNameParam)
                                        .WithParameter(redisKeyVaultNameKey, redisBuilder.GetOutput(redisKeyVaultNameKey));

        // create dapr resource builder (with dapr component output)
        var daprResourceBuilder = builder.CreateDaprResourceBuilder([redisHostParam], out var daprComponent)
                                        .WithParameter(secretStoreComponentKey, secretStoreBuilder.GetOutput(secretStoreComponentKey))
                                        .WithParameter(redisHostKey, redisBuilder.GetOutput(daprConnectionStringKey));

        // set dapr component secret store component
        daprComponent.SecretStoreComponent = secretStoreComponent;

        // Handle
        Action<AzureRedisResource, AzureResourceInfrastructure> additionalConfigurationAction =
                    static (redisResource, infrastructure) =>
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
                    };

        redisBuilder.ConfigureComponentMetadata(daprComponent, redisHostParam, [
            new ContainerAppDaprMetadata { Name = "redisPassword", SecretRef = "redis-password" }
        ], additionalConfigurationAction);
    }

    private static IResourceBuilder<AzureDaprComponentResource> CreateDaprResourceBuilder(
        this IResourceBuilder<IDaprComponentResource> builder, IEnumerable<ProvisioningParameter> provisioningParameters,
        out ContainerAppManagedEnvironmentDaprComponent daprComponent)
    {
        // Create the base Dapr component.
        daprComponent = AzureDaprHostingExtensions.CreateDaprComponent(
            redisDaprComponent,
            builder.Resource.Name,
            "state.redis",
            "v1");

        // Set up infrastructure configuration for the Dapr component.
        var configureInfrastructure = builder.GetInfrastructureConfigurationAction(daprComponent, provisioningParameters, true);


        // Create the Dapr resource builder
        return builder.AddAzureDaprResource(redisDaprComponent, configureInfrastructure);
    }

    private static void ConfigureComponentMetadata(this IResourceBuilder<AzureRedisCacheResource> builder,
        ContainerAppManagedEnvironmentDaprComponent daprComponent, ProvisioningParameter redisHostParam,
        IEnumerable<ContainerAppDaprMetadata> metadata, Action<AzureRedisResource, AzureResourceInfrastructure>? additionalConfigurationAction = null)
    {
        builder.ConfigureInfrastructure(infrastructure =>
        {
            var redisResource = infrastructure.GetProvisionableResources().OfType<AzureRedisResource>().Single();

            bool enableTLS = !redisResource.EnableNonSslPort.Value;
            BicepValue<int> port = enableTLS ? redisResource.SslPort : redisResource.Port;
            infrastructure.Add(new ProvisioningOutput(daprConnectionStringKey, typeof(string))
            {
                Value = BicepFunction.Interpolate($"{redisResource.HostName}:{port}")
            });

            daprComponent.Metadata = [
                new ContainerAppDaprMetadata { Name = redisHostKey, Value = redisHostParam },
                new ContainerAppDaprMetadata { Name = "enableTLS", Value = enableTLS ? "true" : "false" },
                new ContainerAppDaprMetadata { Name = "actorStateStore", Value = "true" },
                ..metadata
            ];
            if (additionalConfigurationAction is not null)
            {
                additionalConfigurationAction(redisResource, infrastructure);
            }
        });
    }
}
