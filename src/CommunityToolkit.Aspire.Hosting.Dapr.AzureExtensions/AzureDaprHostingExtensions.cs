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
    public static IResourceBuilder<AzureDaprComponentResource> AddAzureDaprResource(
        this IResourceBuilder<IDaprComponentResource> builder,
        [ResourceName] string name,
        Action<AzureResourceInfrastructure> configureInfrastructure)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        ArgumentNullException.ThrowIfNull(configureInfrastructure, nameof(configureInfrastructure));

        builder.ExcludeFromManifest();

        var azureDaprComponentResource = new AzureDaprComponentResource(name, configureInfrastructure);

        return builder.ApplicationBuilder
                                    .AddResource(azureDaprComponentResource)
                                    .WithManifestPublishingCallback(azureDaprComponentResource.WriteToManifest);
    }

    /// <summary>
    /// Configures the infrastructure for a Dapr component in a container app managed environment.
    /// </summary>
    /// <param name="daprComponent">The Dapr component to configure.</param>
    /// <param name="parameters">The parameters to provide to the component</param>
    /// <returns>An action to configure the Azure resource infrastructure.</returns>
    public static Action<AzureResourceInfrastructure> GetInfrastructureConfigurationAction(
        ContainerAppManagedEnvironmentDaprComponent daprComponent,
        IEnumerable<ProvisioningParameter>? parameters = null) =>
        (AzureResourceInfrastructure infrastructure) =>
        {
            ArgumentNullException.ThrowIfNull(daprComponent, nameof(daprComponent));
            ArgumentNullException.ThrowIfNull(infrastructure, nameof(infrastructure));

            ProvisioningVariable resourceToken = new("resourceToken", typeof(string))
            {
                Value = BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id)
            };

            infrastructure.Add(resourceToken);

            var containerAppEnvironment = ContainerAppManagedEnvironment.FromExisting("containerAppEnvironment");
            containerAppEnvironment.Name = BicepFunction.Interpolate($"cae-{resourceToken}");

            infrastructure.Add(containerAppEnvironment);
            daprComponent.Parent = containerAppEnvironment;

            if (!daprComponent.ProvisionableProperties.TryGetValue("Name", out IBicepValue? name) || name.IsEmpty)
            {
                daprComponent.Name = BicepFunction.Take(BicepFunction.Interpolate($"{daprComponent.BicepIdentifier}{resourceToken}"), 24);
            }

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
        ArgumentNullException.ThrowIfNull(infrastructure, nameof(infrastructure));

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
    /// <param name="bicepIdentifier">The name of the resource.</param>
    /// <param name="componentType">The type of the Dapr component.</param>
    /// <param name="version">The version of the Dapr component.</param>
    /// <returns>A new instance of <see cref="ContainerAppManagedEnvironmentDaprComponent"/>.</returns>
    public static ContainerAppManagedEnvironmentDaprComponent CreateDaprComponent(
        string bicepIdentifier,
        string componentType,
        string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(bicepIdentifier, nameof(bicepIdentifier));
        ArgumentException.ThrowIfNullOrEmpty(componentType, nameof(componentType));
        ArgumentException.ThrowIfNullOrEmpty(version, nameof(version));
        
        return new(bicepIdentifier)
        {
            ComponentType = componentType,
            Version = version
        };
    }
}
