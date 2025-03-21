// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Humanizer.Localisation;

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
    public static IResourceBuilder<T> WithDaprServiceInvocation<T>(this IResourceBuilder<T> builder, string appId) where T : IResource
    {
        return builder.AddDaprSidecar().WithDaprSidecarOptions(new DaprSidecarOptions { AppId = appId });
    }

    /// <summary>
    /// Ensures that a Dapr sidecar is started for the resource.
    /// </summary>
    /// <typeparam name="T">The type of the resource.</typeparam>
    /// <param name="builder">The resource builder instance.</param>
    /// <param name="options">Options for configuring the Dapr sidecar, if any.</param>
    /// <returns>The resource builder instance.</returns>
    public static IResourceBuilder<T> WithDaprServiceInvocation<T>(this IResourceBuilder<T> builder, DaprSidecarOptions? options = null) where T : IResource
    {
        return builder.AddDaprSidecar().WithDaprSidecarOptions(options ?? new());
    }

    /// <summary>
    /// Configure options for the project 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IResourceBuilder<T> WithDaprSidecarOptions<T>(this IResourceBuilder<T> builder, DaprSidecarOptions options) where T : IResource
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
        // If we're adding a component - then we also need a sidecar. 
        return builder.AddDaprSidecar().WithAnnotation(new DaprComponentReferenceAnnotation(component.Resource));
    }


    private static IResourceBuilder<T> AddDaprSidecar<T>(this IResourceBuilder<T> builder) where T : IResource
    {
        // Add Dapr is idempotent, so we can call it multiple times.
        builder.ApplicationBuilder.AddDapr();

        return builder.WithAnnotation(new DaprSidecarAnnotation(new DaprSidecarResource($"{builder.Resource.Name}-dapr")));
    }

    #region Obsolete

    /// <summary>
    /// Ensures that a Dapr sidecar is started for the resource.
    /// </summary>
    /// <typeparam name="T">The type of the resource.</typeparam>
    /// <param name="builder">The resource builder instance.</param>
    /// <param name="appId">The ID for the application, used for service discovery.</param>
    /// <returns>The resource builder instance.</returns>
    [Obsolete($"Use {nameof(WithDaprServiceInvocation)} for service invocation, WithSidecar is no longer required for component references")]
    public static IResourceBuilder<T> WithDaprSidecar<T>(this IResourceBuilder<T> builder, string appId) where T : IResource
    {
        return builder.WithDaprServiceInvocation(appId);
    }


    /// <summary>
    /// Ensures that a Dapr sidecar is started for the resource.
    /// </summary>
    /// <typeparam name="T">The type of the resource.</typeparam>
    /// <param name="builder">The resource builder instance.</param>
    /// <param name="options">Options for configuring the Dapr sidecar, if any.</param>
    /// <returns>The resource builder instance.</returns>
    [Obsolete($"Use {nameof(WithDaprServiceInvocation)} for service invocation, WithSidecar is no longer required for component references")]
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
    [Obsolete]
    public static IResourceBuilder<T> WithDaprSidecar<T>(this IResourceBuilder<T> builder, Action<IResourceBuilder<IDaprSidecarResource>> configureSidecar) where T : IResource
    {
        var sideCarResourceBuilder = builder.ApplicationBuilder.CreateResourceBuilder(new DaprSidecarResource($"{builder.Resource.Name}-dapr"));

        configureSidecar(sideCarResourceBuilder);

        if (sideCarResourceBuilder.Resource.TryGetAnnotationsOfType<DaprSidecarOptionsAnnotation>(out var optionsAnnotations))
        {
            builder.Resource.Annotations.AddRange(optionsAnnotations);
        }

        return builder.WithAnnotation(new DaprSidecarAnnotation(sideCarResourceBuilder.Resource));
    }

    /// <summary>
    /// Configures a Dapr sidecar with the specified options.
    /// </summary>
    /// <param name="builder">The Dapr sidecar resource builder instance.</param>
    /// <param name="options">Options for configuring the Dapr sidecar.</param>
    /// <returns>The Dapr sidecar resource builder instance.</returns>
    [Obsolete($"Use {nameof(WithDaprSidecarOptions)} on the parent resource builder")]
    public static IResourceBuilder<IDaprSidecarResource> WithOptions(this IResourceBuilder<IDaprSidecarResource> builder, DaprSidecarOptions options)
    {
        return builder.WithAnnotation(new DaprSidecarOptionsAnnotation(options));
    }
    #endregion
}
