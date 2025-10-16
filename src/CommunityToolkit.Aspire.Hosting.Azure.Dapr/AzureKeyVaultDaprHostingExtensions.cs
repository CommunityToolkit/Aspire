using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Roles;
using CommunityToolkit.Aspire.Hosting.Azure.Dapr;
using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring Dapr components with Azure Key Vault.
/// </summary>
public static class AzureKeyVaultDaprHostingExtensions
{
    private const string secretStoreComponentKey = "secretStoreComponent";
    private const string secretStore = nameof(secretStore);



    /// <summary>
    /// Configures the Key Vault secret store component for the Dapr component resource.
    /// </summary>
    /// <param name="builder">The Dapr component resource builder.</param>
    /// <param name="kvNameParam">The Key Vault name parameter.</param>
    /// <returns>The original Dapr component resource builder (not a new Azure Dapr resource).</returns>
    public static IResourceBuilder<IDaprComponentResource> ConfigureKeyVaultSecretsComponent(this IResourceBuilder<IDaprComponentResource> builder, ProvisioningParameter kvNameParam)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        //TODO: We may need to actually add the key vault resource here as well - I'm not sure if aspire automatically adds it anymore or not

        var configureInfrastructure = (AzureResourceInfrastructure infrastructure, UserAssignedIdentity daprIdentity) =>
        {
            if (infrastructure.GetProvisionableResources().OfType<ContainerAppManagedEnvironment>().FirstOrDefault() is ContainerAppManagedEnvironment managedEnvironment)
            {
                var daprComponent = AzureDaprHostingExtensions.CreateDaprComponent(
                    secretStore,
                    BicepFunction.Interpolate($"{builder.Resource.Name}-secretstore"),
                    "secretstores.azure.keyvault",
                    "v1");

                daprComponent.Parent = managedEnvironment;
                daprComponent.Scopes = [];
                daprComponent.Metadata = [
                    new ContainerAppDaprMetadata { Name = "vaultName", Value = kvNameParam },
                    new ContainerAppDaprMetadata { Name = "azureClientId", Value = daprIdentity.PrincipalId }
                ];

                infrastructure.Add(daprComponent);
                infrastructure.Add(kvNameParam);

                infrastructure.Add(new ProvisioningOutput(secretStoreComponentKey, typeof(string))
                {
                    Value = daprComponent.Name
                });
            }
        };

        // Add the publishing annotation to the original Dapr component resource
        // This ensures the Key Vault secret store gets created when publishing
        builder.WithAnnotation(new AzureDaprComponentPublishingAnnotation(configureInfrastructure));

        return builder;
    }
}
