using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Dapr;
using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring Dapr components in an Azure hosting environment.
/// </summary>
public static class DaprAzureHostingExtensions
{
    /// <summary>
    /// Configures a Dapr component for use in an Azure environment.
    /// </summary>
    /// <param name="builder">The builder for the Dapr component resource.</param>
    /// <param name="source">The builder for the Azure provisioning resource.</param>
    /// <param name="componentType">The type of the Dapr component.</param>
    /// <param name="version">The version of the Dapr component.</param>
    /// <param name="configureDaprResource">An action to configure the Dapr resource.</param>
    /// <returns>The builder for the Dapr component resource.</returns>
    public static IResourceBuilder<IDaprComponentResource> ConfigureForAzure(
        this IResourceBuilder<IDaprComponentResource> builder,
        IResourceBuilder<AzureProvisioningResource> source, string componentType, string version,
        Func<AzureResourceInfrastructure, ContainerAppManagedEnvironmentDaprComponent, IEnumerable<ProvisioningParameter>> configureDaprResource)
    {
        var daprComponent = builder.InitializeDaprComponent(componentType, version);

        source.ConfigureInfrastructure(module =>
        {
            builder.AddAzureInfrasructure(nameof(daprComponent), daprComponent, configureDaprResource(module, daprComponent));
        });

        return builder;
    }

    private static IResourceBuilder<IDaprComponentResource> AddAzureInfrasructure(
        this IResourceBuilder<IDaprComponentResource> builder, string name,
        ContainerAppManagedEnvironmentDaprComponent daprComponent,
        IEnumerable<ProvisioningParameter> parameters)
    {
        var resourceToken = BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id);
        var containerAppEnvironment = ContainerAppManagedEnvironment.FromExisting("containerAppEnvironment");
        containerAppEnvironment.Name = BicepFunction.Interpolate($"cae-{resourceToken}");

        daprComponent.Parent = containerAppEnvironment;

        builder.ApplicationBuilder.AddAzureInfrastructure(name, infrastructure =>
        {
            infrastructure.Add(containerAppEnvironment);
            infrastructure.Add(daprComponent);
            foreach (var parameter in parameters)
            {
                infrastructure.Add(parameter);
            }
        });

        return builder.ExcludeFromManifest();
    }

    private static ContainerAppManagedEnvironmentDaprComponent InitializeDaprComponent(
        this IResourceBuilder<IDaprComponentResource> builder,
        string componentType,
        string version) => new(builder.Resource.Name)
        {
            ComponentType = componentType,
            Version = version
        };
}
