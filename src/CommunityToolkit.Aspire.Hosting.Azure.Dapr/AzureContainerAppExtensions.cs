using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Aspire.Hosting;

/// <summary>
/// Resource builder extensions for publishing to Azure
/// </summary>
public static class AzureContainerAppExtensions
{

    /// <summary>
    /// Explicit call is required when any project / container uses PublishAsAzureContainerApp
    /// TODO: Validate whether this can be called implicitly
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IResourceBuilder<T> PublishWithDaprSidecar<T>(this IResourceBuilder<T> builder) where T : ContainerResource
    {
        return builder.PublishAsAzureContainerApp((infrastructure, containerApp) => infrastructure.AddDaprSidecarInfrastructure(containerApp));
    }
    /// <summary>
    /// Explicit call is required when any project / container uses PublishAsAzureContainerApp
    /// TODO: Validate whether this can be called implicitly
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IResourceBuilder<ProjectResource> PublishWithDaprSidecar(this IResourceBuilder<ProjectResource> builder)
    {
        return builder.PublishAsAzureContainerApp((infrastructure, containerApp) => infrastructure.AddDaprSidecarInfrastructure(containerApp));
    }

    private static AzureResourceInfrastructure AddDaprSidecarInfrastructure(this AzureResourceInfrastructure infrastructure, ContainerApp containerApp)
    {
        // I need to get the dapr sidecar
        if (infrastructure.AspireResource.TryGetLastAnnotation<DaprSidecarOptionsAnnotation>(out var daprSidecarOptionsAnnotation))
        {
            var daprSidecarOptions = daprSidecarOptionsAnnotation.Options;
            var daprConfiguration = new ContainerAppDaprConfiguration
            {
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
            if (!string.IsNullOrWhiteSpace(daprSidecarOptions?.AppId))
            {
                daprConfiguration.AppId = daprSidecarOptions.AppId;
            }
            else if (containerApp.Template.Containers.FirstOrDefault() is BicepValue<ContainerAppContainer> container
                && container.Value is not null)
            {
                daprConfiguration.AppId = container.Value.Name; // not sure if this is the right fallback - need to validate when testing
            }
            containerApp.Configuration.Dapr = daprConfiguration;
        }
        return infrastructure;
    }

}
