// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;

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
    /// <remarks>This overload is not available in polyglot app hosts. Use the generated <c>withDaprSidecar</c> overload that accepts a Dapr sidecar configuration object instead.</remarks>
    [AspireExportIgnore(Reason = "Use the options-based overload instead to avoid ambiguous polyglot overloads.")]
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
    /// <remarks>This overload is not available in polyglot app hosts. Use the generated <c>withDaprSidecar</c> overload that accepts a Dapr sidecar configuration object instead.</remarks>
    [AspireExportIgnore(Reason = "Use the exported DTO-based overload instead to avoid ambiguous polyglot wrapper generation.")]
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

    [AspireExport("withDaprSidecar", MethodName = "withDaprSidecar", Description = "Adds a Dapr sidecar to the resource and optionally configures it")]
    internal static IResourceBuilder<T> WithDaprSidecarExport<T>(this IResourceBuilder<T> builder, DaprSidecarExportOptions? sidecarOptions = null) where T : IResource
    {
        return builder.WithDaprSidecar(sidecarOptions?.ToDaprSidecarOptions());
    }

    /// <summary>
    /// Ensures that a Dapr sidecar is started for the resource.
    /// </summary>
    /// <typeparam name="T">The type of the resource.</typeparam>
    /// <param name="builder">The resource builder instance.</param>
    /// <param name="configureSidecar">A callback that can be use to configure the Dapr sidecar.</param>
    /// <returns>The resource builder instance.</returns>
    [AspireExport("configureDaprSidecar", MethodName = "configureDaprSidecar", Description = "Adds a Dapr sidecar to the resource and exposes it for callback configuration")]
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
                                                       });



        configureSidecar(sidecarBuilder);


        return builder.WithAnnotation(new DaprSidecarAnnotation(sidecarBuilder.Resource));
    }

    /// <summary>
    /// Configures a Dapr sidecar with the specified options.
    /// </summary>
    /// <param name="builder">The Dapr sidecar resource builder instance.</param>
    /// <param name="options">Options for configuring the Dapr sidecar.</param>
    /// <returns>The Dapr sidecar resource builder instance.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use the generated <c>withOptions</c> overload that accepts a Dapr sidecar configuration object instead.</remarks>
    [AspireExportIgnore(Reason = "Use the exported DTO-based overload instead to avoid ambiguous polyglot wrapper generation.")]
    public static IResourceBuilder<IDaprSidecarResource> WithOptions(this IResourceBuilder<IDaprSidecarResource> builder, DaprSidecarOptions options)
    {
        return builder.WithAnnotation(new DaprSidecarOptionsAnnotation(options));
    }

    [AspireExport("withOptions", MethodName = "withOptions", Description = "Configures options for a Dapr sidecar resource")]
    internal static IResourceBuilder<IDaprSidecarResource> WithOptionsExport(this IResourceBuilder<IDaprSidecarResource> builder, DaprSidecarExportOptions sidecarOptions)
    {
        return builder.WithOptions(sidecarOptions.ToDaprSidecarOptions());
    }

    /// <summary>
    /// Associates a Dapr component with the Dapr sidecar started for the resource.
    /// </summary>
    /// <typeparam name="TDestination">The type of the resource.</typeparam>
    /// <param name="builder">The resource builder instance.</param>
    /// <param name="component">The Dapr component to use with the sidecar.</param>
    [Obsolete("Add reference to the sidecar resource instead of the project resource")]
    public static IResourceBuilder<TDestination> WithReference<TDestination>(this IResourceBuilder<TDestination> builder, IResourceBuilder<IDaprComponentResource> component) where TDestination : IResource
    {
        return builder.WithAnnotation(new DaprComponentReferenceAnnotation(component.Resource));
    }
    /// <summary>
    /// Associates a Dapr component with the Dapr sidecar started for the resource.
    /// </summary>
    /// <param name="builder">The resource builder instance.</param>
    /// <param name="component">The Dapr component to use with the sidecar.</param>
    [AspireExport("withReference", Description = "Associates a Dapr component with a Dapr sidecar resource")]
    public static IResourceBuilder<IDaprSidecarResource> WithReference(this IResourceBuilder<IDaprSidecarResource> builder, IResourceBuilder<IDaprComponentResource> component)
    {
        return builder.WithAnnotation(new DaprComponentReferenceAnnotation(component.Resource));
    }
}
