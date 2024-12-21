using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Dapr;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;
using Azure.Provisioning;
using Azure.Provisioning.KeyVault;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring Dapr components in an Azure hosting environment.
/// </summary>
public static class AzureDaprHostingExtensions
{
    /// <summary>
    /// Adds an Azure Dapr resource to the resource builder.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the Dapr resource.</param>
    /// <param name="configureInfrastructure">The action to configure the Azure resource infrastructure.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<AzureProvisioningResource> AddAzureDaprResource(
        this IResourceBuilder<IDaprComponentResource> builder,
        [ResourceName] string name,
        Action<AzureResourceInfrastructure> configureInfrastructure)
    {
        // Validate if this is needed
        builder.ExcludeFromManifest();
        // Create a resource to wrap this
        return builder.ApplicationBuilder.AddResource(new AzureDaprComponentResource(name, configureInfrastructure));
    }

    /// <summary>
    /// Configures the infrastructure for a Dapr component in a container app managed environment.
    /// </summary>
    /// <param name="daprComponent">The Dapr component to configure.</param>
    /// <param name="parameters">The parameters to provide to the component</param>
    /// <returns>An action to configure the Azure resource infrastructure.</returns>
    public static Action<AzureResourceInfrastructure> GetInfrastructureConfigurationAction(
        ContainerAppManagedEnvironmentDaprComponent daprComponent,
        IEnumerable<ProvisioningParameter> parameters) =>
        (AzureResourceInfrastructure infrastructure) =>
        {
            var resourceToken = BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id);
            var containerAppEnvironment = ContainerAppManagedEnvironment.FromExisting("containerAppEnvironment");
            containerAppEnvironment.Name = BicepFunction.Interpolate($"cae-{resourceToken}");

            infrastructure.Add(containerAppEnvironment);
            daprComponent.Parent = containerAppEnvironment;
            infrastructure.Add(daprComponent);

            foreach (var parameter in parameters ?? [])
            {
                infrastructure.Add(parameter);
            }
        };


    /// <summary>
    /// Configures Key Vault secrets for the Azure resource infrastructure.
    /// </summary>
    /// <param name="infrastructure">The Azure resource infrastructure.</param>
    /// <param name="keyVaultSecrets">The Key Vault secrets to configure.</param>
    /// <returns>The configured Key Vault service.</returns>
    public static KeyVaultService ConfigureKeyVaultSecrets(
        this AzureResourceInfrastructure infrastructure, IEnumerable<KeyVaultSecret>? keyVaultSecrets = null)
    {
        var kvNameParam = new ProvisioningParameter("keyVaultName", typeof(string));
        infrastructure.Add(kvNameParam);

        var keyVault = KeyVaultService.FromExisting("keyVault");
        keyVault.Name = kvNameParam;
        infrastructure.Add(keyVault);

        foreach (var secret in keyVaultSecrets ?? [])
        {
            secret.Parent = keyVault;
            infrastructure.Add(secret);
        }
        return keyVault;
    }
    /// <summary>
    /// Creates a new Dapr component for a container app managed environment.
    /// </summary>
    /// <param name="resourceName">The name of the resource.</param>
    /// <param name="componentType">The type of the Dapr component.</param>
    /// <param name="version">The version of the Dapr component.</param>
    /// <returns>A new instance of <see cref="ContainerAppManagedEnvironmentDaprComponent"/>.</returns>
    public static ContainerAppManagedEnvironmentDaprComponent CreateDaprComponent(
        string resourceName,
        string componentType,
        string version) => new(resourceName)
        {
            ComponentType = componentType,
            Version = version
        };

}
