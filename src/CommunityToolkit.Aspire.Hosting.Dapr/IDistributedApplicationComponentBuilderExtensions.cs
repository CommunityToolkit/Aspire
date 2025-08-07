// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Extensions to <see cref="IResourceBuilder{T}"/> related to Dapr.
/// </summary>
public static class IDistributedApplicationResourceBuilderExtensions
{
    /// <summary>
    /// Ensures that a Dapr sidecar is started for the resource.
    /// </summary>
    /// <typeparam name="T">The type of the resource.</typeparam>
    /// <param name="builder">The resource builder instance.</param>
    /// <param name="appId">The ID for the application, used for service discovery.</param>
    /// <returns>The resource builder instance.</returns>
    public static IResourceBuilder<T> WithDaprSidecar<T>(this IResourceBuilder<T> builder, string appId) where T : IResource
    {
        return builder.WithDaprSidecar(new DaprSidecarOptions { AppId = appId });
    }

    /// <summary>
    /// Ensures that a Dapr sidecar is started for the resource.
    /// </summary>
    /// <typeparam name="T">The type of the resource.</typeparam>
    /// <param name="builder">The resource builder instance.</param>
    /// <param name="options">Options for configuring the Dapr sidecar, if any.</param>
    /// <returns>The resource builder instance.</returns>
    public static IResourceBuilder<T> WithDaprSidecar<T>(this IResourceBuilder<T> builder, DaprSidecarOptions? options = null) where T : IResource
    {
        return builder.WithDaprSidecar(
            sidecarBuilder =>
            {
                if (options is not null)
                {
                    sidecarBuilder.WithOptions(options);
                }
            });
    }

    /// <summary>
    /// Ensures that a Dapr sidecar is started for the resource.
    /// </summary>
    /// <typeparam name="T">The type of the resource.</typeparam>
    /// <param name="builder">The resource builder instance.</param>
    /// <param name="configureSidecar">A callback that can be use to configure the Dapr sidecar.</param>
    /// <returns>The resource builder instance.</returns>
    public static IResourceBuilder<T> WithDaprSidecar<T>(this IResourceBuilder<T> builder, Action<IResourceBuilder<IDaprSidecarResource>> configureSidecar) where T : IResource
    {
        // Add Dapr is idempotent, so we can call it multiple times.
        builder.ApplicationBuilder.AddDapr();

        var sidecarBuilder = builder.ApplicationBuilder.AddResource(new DaprSidecarResource($"{builder.Resource.Name}-dapr"))
                                                       .WithInitialState(new()
                                                       {
                                                           Properties = [],
                                                           ResourceType = "DaprSidecar",
                                                           IsHidden = true,
                                                           State = KnownResourceStates.NotStarted
                                                       });

        configureSidecar(sidecarBuilder);

        SetupSidecarLifecycle(builder, sidecarBuilder);

        return builder.WithAnnotation(new DaprSidecarAnnotation(sidecarBuilder.Resource));
    }

    private static void SetupSidecarLifecycle<T>(IResourceBuilder<T> parentBuilder, IResourceBuilder<IDaprSidecarResource> sidecarBuilder) where T : IResource
    {
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var valueProviderResources = new List<IResourceBuilder<IResource>>();
        
        // Find component references from the parent resource
        if (parentBuilder.Resource.TryGetAnnotationsOfType<DaprComponentReferenceAnnotation>(out var componentRefs))
        {
            foreach (var componentRef in componentRefs)
            {
                // Check for value provider annotations on the component
                if (componentRef.Component.TryGetAnnotationsOfType<DaprComponentValueProviderAnnotation>(out var valueProviderAnnotations))
                {
                    foreach (var annotation in valueProviderAnnotations)
                    {
                        // Extract resource references from value providers
                        if (annotation.ValueProvider is IResourceWithoutLifetime)
                        {
                            // Skip waiting for resources without a lifetime
                            continue;
                        }

                        if (annotation.ValueProvider is IResource resource)
                        {
                            if (dependencies.Add(resource.Name))
                            {
                                // Create resource builder for waiting
                                var resourceBuilder = parentBuilder.ApplicationBuilder.CreateResourceBuilder(resource);
                                valueProviderResources.Add(resourceBuilder);
                                // Add wait dependency using WaitFor (waits for resource to be available/running)
                                sidecarBuilder.WaitFor(resourceBuilder);
                            }
                        }
                        else if (annotation.ValueProvider is IValueWithReferences valueWithReferences)
                        {
                            foreach (var innerRef in valueWithReferences.References.OfType<IResource>())
                            {
                                if (dependencies.Add(innerRef.Name))
                                {
                                    // Create resource builder for waiting
                                    var resourceBuilder = parentBuilder.ApplicationBuilder.CreateResourceBuilder(innerRef);
                                    valueProviderResources.Add(resourceBuilder);
                                    // Add wait dependency using WaitFor (waits for resource to be available/running)
                                    sidecarBuilder.WaitFor(resourceBuilder);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Hook into the sidecar initialization for state management and event publishing
        sidecarBuilder.OnInitializeResource(async (sidecar, evt, ct) =>
        {
            try
            {
                // Update state to starting
                await evt.Notifications.PublishUpdateAsync(sidecar, s => s with
                {
                    State = KnownResourceStates.Starting
                }).ConfigureAwait(false);

                // Publish before started event
                await evt.Eventing.PublishAsync(new BeforeResourceStartedEvent(sidecar, evt.Services), ct).ConfigureAwait(false);

                // Update state to running
                await evt.Notifications.PublishUpdateAsync(sidecar, s => s with
                {
                    State = KnownResourceStates.Running
                }).ConfigureAwait(false);

                // Publish sidecar available event
                await evt.Eventing.PublishAsync(new DaprSidecarAvailableEvent(sidecar, evt.Services), ct).ConfigureAwait(false);
                
                evt.Logger.LogInformation("Dapr sidecar '{SidecarName}' started successfully", sidecar.Name);
            }
            catch (Exception ex)
            {
                evt.Logger.LogError(ex, "Failed to initialize Dapr sidecar '{SidecarName}'", sidecar.Name);

                // Update state to failed
                await evt.Notifications.PublishUpdateAsync(sidecar, s => s with
                {
                    State = KnownResourceStates.FailedToStart
                }).ConfigureAwait(false);
            }
        });
    }

    /// <summary>
    /// Configures a Dapr sidecar with the specified options.
    /// </summary>
    /// <param name="builder">The Dapr sidecar resource builder instance.</param>
    /// <param name="options">Options for configuring the Dapr sidecar.</param>
    /// <returns>The Dapr sidecar resource builder instance.</returns>
    public static IResourceBuilder<IDaprSidecarResource> WithOptions(this IResourceBuilder<IDaprSidecarResource> builder, DaprSidecarOptions options)
    {
        return builder.WithAnnotation(new DaprSidecarOptionsAnnotation(options));
    }

    /// <summary>
    /// Associates a Dapr component with the Dapr sidecar started for the resource.
    /// </summary>
    /// <typeparam name="TDestination">The type of the resource.</typeparam>
    /// <param name="builder">The resource builder instance.</param>
    /// <param name="component">The Dapr component to use with the sidecar.</param>
    public static IResourceBuilder<TDestination> WithReference<TDestination>(this IResourceBuilder<TDestination> builder, IResourceBuilder<IDaprComponentResource> component) where TDestination : IResource
    {
        return builder.WithAnnotation(new DaprComponentReferenceAnnotation(component.Resource));
    }
}
