using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Dapr;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;

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
    /// <param name="configurInfrastructure">The action to configure the Azure resource infrastructure.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<IDaprComponentResource> AddAzureDaprResource(
        this IResourceBuilder<IDaprComponentResource> builder,
        [ResourceName] string name,
        Action<AzureResourceInfrastructure> configurInfrastructure)
    {
        var daprResource = new AzureDaprResource(name, configurInfrastructure);
        builder.ApplicationBuilder.AddResource(daprResource);

        return builder.ExcludeFromManifest();
    }

    /// <summary>
    /// Configures the infrastructure for a Dapr component in a container app managed environment.
    /// </summary>
    /// <param name="daprComponent">The Dapr component to configure.</param>
    /// <returns>An action to configure the Azure resource infrastructure.</returns>
    public static Action<AzureResourceInfrastructure> ConfigureInfrastructure(ContainerAppManagedEnvironmentDaprComponent daprComponent) =>
        (AzureResourceInfrastructure infrastructure) =>
        {
            var resourceToken = BicepFunction.GetUniqueString(BicepFunction.GetResourceGroup().Id);
            var containerAppEnvironment = ContainerAppManagedEnvironment.FromExisting("containerAppEnvironment");
            containerAppEnvironment.Name = BicepFunction.Interpolate($"cae-{resourceToken}");

            infrastructure.Add(containerAppEnvironment);

            infrastructure.Add(daprComponent);
        };

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
