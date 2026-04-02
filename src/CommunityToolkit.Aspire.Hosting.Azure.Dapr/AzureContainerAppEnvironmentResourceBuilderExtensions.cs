using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Azure.AppContainers;
using Azure.Provisioning.AppContainers;
using CommunityToolkit.Aspire.Hosting.Azure.Dapr;
using CommunityToolkit.Aspire.Hosting.Dapr;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring Azure Container App Environment resources.
/// </summary>
public static class AzureContainerAppEnvironmentResourceBuilderExtensions
{
    /// <summary>
    /// Configures the Azure Container App Environment resource to use Dapr.
    /// </summary>
    /// <param name="builder">The Azure Container App Environment resource builder.</param>
    /// <returns>The updated Azure Container App Environment resource builder.</returns>
    [AspireExport("withDaprComponents", Description = "Configures an Azure Container App environment to publish Dapr components")]
    public static IResourceBuilder<AzureContainerAppEnvironmentResource> WithDaprComponents(
        this IResourceBuilder<AzureContainerAppEnvironmentResource> builder)
    {
        builder.ApplicationBuilder.AddDapr(c =>
       {
           c.PublishingConfigurationAction = (IResource resource, DaprSidecarOptions? daprSidecarOptions) =>
           {
               var configureAction = (AzureResourceInfrastructure infrastructure, ContainerApp containerApp) =>
               {
                   containerApp.Configuration.Dapr = new ContainerAppDaprConfiguration
                   {
                       AppId = daprSidecarOptions?.AppId ?? resource.Name,
                       AppPort = daprSidecarOptions?.AppPort ?? 8080,
                       IsApiLoggingEnabled = daprSidecarOptions?.EnableApiLogging ?? false,
                       LogLevel = daprSidecarOptions?.LogLevel?.ToLower() switch
                       {
                           "debug" => ContainerAppDaprLogLevel.Debug,
                           "warn" => ContainerAppDaprLogLevel.Warn,
                           "error" => ContainerAppDaprLogLevel.Error,
                           _ => ContainerAppDaprLogLevel.Info
                       },
                       AppProtocol = daprSidecarOptions?.AppProtocol?.ToLower() switch
                       {
                           "grpc" => ContainerAppProtocol.Grpc,
                           _ => ContainerAppProtocol.Http,
                       },
                       IsEnabled = true
                   };
               };

               resource.Annotations.Add(new AzureContainerAppCustomizationAnnotation(configureAction));
           };
       });

        return builder.ConfigureInfrastructure(infrastructure =>
        {
            var daprComponentResources = builder.ApplicationBuilder.Resources.OfType<IDaprComponentResource>();

            foreach (var daprComponentResource in daprComponentResources)
            {
                daprComponentResource.TryGetLastAnnotation<AzureDaprComponentPublishingAnnotation>(out var publishingAnnotation);

                publishingAnnotation?.PublishingAction(infrastructure);
            }
        });
    }
}
