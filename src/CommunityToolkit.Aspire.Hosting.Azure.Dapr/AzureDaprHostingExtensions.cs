using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Expressions;
using Azure.Provisioning;
using CommunityToolkit.Aspire.Hosting.Dapr;

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
    /// Adds scopes to the specified Dapr component in a container app managed environment.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="daprComponent">The Dapr component to add scopes to.</param>
    public static void AddScopes(this IResourceBuilder<IDaprComponentResource> builder, ContainerAppManagedEnvironmentDaprComponent daprComponent)
    {
        daprComponent.Scopes = [];

        foreach (var resource in builder.ApplicationBuilder.Resources)
        {
            if (!resource.TryGetLastAnnotation<DaprSidecarAnnotation>(out var daprAnnotation) ||
            !resource.TryGetAnnotationsOfType<DaprComponentReferenceAnnotation>(out var daprComponentReferenceAnnotations))
            {
                continue;
            }

            foreach (var reference in daprComponentReferenceAnnotations)
            {
                if (reference.Component.Name == builder.Resource.Name)
                {
                    var daprSidecar = daprAnnotation.Sidecar;
                    var sidecarOptionsAnnotation = daprSidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().LastOrDefault();

                    var sidecarOptions = sidecarOptionsAnnotation?.Options;

                    var appId = sidecarOptions?.AppId ?? resource.Name;
                    daprComponent.Scopes.Add(appId);
                }
            }
        }
    }

    /// <summary>
    /// Creates a new Dapr component for a container app managed environment.
    /// </summary>
    /// <param name="bicepIdentifier">The name of the resource.</param>
    /// <param name="name">The name of the dapr component</param>
    /// <param name="componentType">The type of the Dapr component.</param>
    /// <param name="version">The version of the Dapr component.</param>
    /// <returns>A new instance of <see cref="ContainerAppManagedEnvironmentDaprComponent"/>.</returns>
    public static ContainerAppManagedEnvironmentDaprComponent CreateDaprComponent(
        string bicepIdentifier,
        BicepValue<string> name,
        string componentType,
        string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(bicepIdentifier, nameof(bicepIdentifier));
        ArgumentException.ThrowIfNullOrEmpty(componentType, nameof(componentType));
        ArgumentException.ThrowIfNullOrEmpty(version, nameof(version));

        return new(bicepIdentifier)
        {
            Name = name,
            ComponentType = componentType,
            Version = version
        };
    }
}
