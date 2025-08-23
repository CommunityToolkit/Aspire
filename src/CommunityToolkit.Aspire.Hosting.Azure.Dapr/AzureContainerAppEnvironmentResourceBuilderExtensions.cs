using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure.AppContainers;
using Azure.Provisioning.AppContainers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CommunityToolkit.Aspire.Hosting.Azure.Dapr;

/// <summary>
/// Provides extension methods for configuring Azure Container App Environment resources.
/// </summary>
public static class AzureContainerAppEnvironmentResourceBuilderExtensions
{
    /// <summary>
    /// Configures the Azure Container App Environment resource to use Dapr.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IResourceBuilder<AzureContainerAppEnvironmentResource> WithDapr(
        this IResourceBuilder<AzureContainerAppEnvironmentResource> builder)
    {
        //TODO: Implement Dapr configuration for Azure Container App Environment
        return builder.ConfigureInfrastructure(infr =>
        {
            if (infr.GetProvisionableResources().OfType<ContainerAppManagedEnvironment>().FirstOrDefault() is ContainerAppManagedEnvironment managedEnvironment)
            {
                ContainerAppManagedEnvironmentDaprComponent containerAppManagedEnvironmentDaprComponent = new("cae_123_dapr")
                {
                    Parent = managedEnvironment,
                };
                containerAppManagedEnvironmentDaprComponent.Scopes = ["service-a", "service-b", "service-c"];

                infr.Add(containerAppManagedEnvironmentDaprComponent);
            }
        });
    }
}
