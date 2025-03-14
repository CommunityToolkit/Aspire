using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.KeyVault;
using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring Dapr components with Azure Redis.
/// </summary>
public static class AzureKeyVaultDaprHostingExtensions
{
    private const string secretStoreComponentKey = "secretStoreComponent";
    private const string secretStore = nameof(secretStore);

    /// <summary>
    /// Configures the Redis state component for the Dapr component resource.
    /// </summary>
    /// <param name="builder">The Dapr component resource builder.</param>
    /// <param name="kvNameParam"></param>
    /// <returns>The new Dapr component resource builder.</returns>
    public static IResourceBuilder<AzureDaprComponentResource> ConfigureKeyVaultSecretsComponent(
        this IResourceBuilder<IDaprComponentResource> builder, ProvisioningParameter kvNameParam)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        var principalIdParameter = new ProvisioningParameter(AzureBicepResource.KnownParameters.PrincipalId, typeof(string));

        var daprComponent = AzureDaprHostingExtensions.CreateDaprComponent(
            secretStore,
            BicepFunction.Interpolate($"{builder.Resource.Name}-secretstore"),
            "secretstores.azure.keyvault",
            "v1");
            
        daprComponent.Scopes = [];

        var configureInfrastructure = builder.GetInfrastructureConfigurationAction(daprComponent, [principalIdParameter]);

        return builder.AddAzureDaprResource(secretStore, configureInfrastructure).ConfigureInfrastructure(infrastructure =>
        {
            daprComponent.Metadata = [
                new ContainerAppDaprMetadata { Name = "vaultName", Value = kvNameParam },
                new ContainerAppDaprMetadata { Name = "azureClientId", Value = principalIdParameter }
            ];

            infrastructure.Add(kvNameParam);

            infrastructure.Add(new ProvisioningOutput(secretStoreComponentKey, typeof(string))
            {
                Value = daprComponent.Name
            });
        });
    }
}
