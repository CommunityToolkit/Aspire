using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning.AppContainers;
using Azure.ResourceManager.KeyVault;
using CommunityToolkit.Aspire.Hosting.Dapr;

namespace CommunityToolkit.Aspire.Hosting.Azure.Dapr;

internal class AzureDaprPublishingHelper : IDaprPublishingHelper
{
    public ValueTask ExecuteProviderSpecificRequirements(
        DistributedApplicationModel appModel,
        IResource resource,
        DaprSidecarOptions? daprSidecarOptions,
        CancellationToken cancellationToken)
    {
        if (appModel.Resources.Any(r => r.HasAnnotationOfType<AzureContainerAppCustomizationAnnotation>()))
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
        }

        return ValueTask.CompletedTask;
    }
}
