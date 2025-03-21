using Aspire.Hosting.ApplicationModel;
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
    /// <param name="daprSidecarOptions"></param>
    /// <returns></returns>
    public static IResourceBuilder<ProjectResource> PublishWithDaprSidecar(this IResourceBuilder<ProjectResource> builder, DaprSidecarOptions? daprSidecarOptions = null)
    {
        return builder.PublishAsAzureContainerApp((infrastructure, containerApp) => containerApp.WithDaprSidecar(daprSidecarOptions));
    }

    /// <summary>
    /// Configure an azure container app for dapr
    /// TODO: Validate if we can call this as part of reference calls (e.g. in azure.dapr.redis library)
    /// </summary>
    /// <param name="containerApp"></param>
    /// <param name="daprSidecarOptions"></param>
    /// <returns></returns>
    public static ContainerApp WithDaprSidecar(this ContainerApp containerApp, DaprSidecarOptions? daprSidecarOptions = null)
    {

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
        return containerApp;
    }
}
