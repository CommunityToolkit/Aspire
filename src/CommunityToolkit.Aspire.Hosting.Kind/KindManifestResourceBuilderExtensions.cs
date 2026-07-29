// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Kind;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIREATS001 // AspireExport APIs are experimental

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Kubernetes manifest resources to Kind clusters.
/// </summary>
public static class KindManifestResourceBuilderExtensions
{
    /// <summary>
    /// Adds a Kubernetes manifest to be applied to the Kind cluster via <c>kubectl apply</c>.
    /// </summary>
    /// <param name="builder">The Kind cluster resource builder.</param>
    /// <param name="name">The name of the manifest resource.</param>
    /// <param name="manifestPath">
    /// Absolute or relative path to a Kubernetes manifest file, a directory of manifest files,
    /// or a Kustomize overlay directory. Relative paths are resolved against the AppHost project directory.
    /// URL fetch is not supported; reference a local file or directory.
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{K8sManifestResource}"/>.</returns>
    /// <remarks>
    /// The manifest is applied after the parent Kind cluster reaches the
    /// <see cref="KnownResourceStates.Running"/> state. Downstream resources that call
    /// <c>WaitFor</c> on the manifest resource only start once <c>kubectl apply</c> succeeds.
    /// Requires <c>kubectl</c> on <c>PATH</c>.
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<K8sManifestResource> AddManifest(
        this IResourceBuilder<KindClusterResource> builder,
        [ResourceName] string name,
        string manifestPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(manifestPath);

        var absolutePath = System.IO.Path.IsPathRooted(manifestPath)
            ? manifestPath
            : System.IO.Path.GetFullPath(
                System.IO.Path.Combine(builder.ApplicationBuilder.AppHostDirectory, manifestPath));

        var resource = new K8sManifestResource(name, absolutePath, builder.Resource);

        return AddManifestResource(builder, resource);
    }

    /// <summary>
    /// Adds Kubernetes manifest content to be applied to the Kind cluster via <c>kubectl apply -f -</c>.
    /// </summary>
    /// <param name="builder">The Kind cluster resource builder.</param>
    /// <param name="name">The name of the manifest resource.</param>
    /// <param name="content">The Kubernetes manifest YAML content.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{K8sManifestResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<K8sManifestResource> AddManifestFromContent(
        this IResourceBuilder<KindClusterResource> builder,
        [ResourceName] string name,
        string content)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(content);

        var resource = new K8sManifestResource(name, K8sManifestResource.InlineManifestPath, builder.Resource)
        {
            InlineContent = content,
        };

        return AddManifestResource(builder, resource);
    }

    private static IResourceBuilder<K8sManifestResource> AddManifestResource(
        IResourceBuilder<KindClusterResource> builder,
        K8sManifestResource resource)
    {
        var resourceBuilder = builder.ApplicationBuilder
            .AddResource(resource)
            .ExcludeFromManifest()
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "K8s Manifest",
                State = KnownResourceStates.NotStarted,
                Properties = [
                    new("ManifestPath", resource.ManifestPath),
                    new("Mode", "apply"),
                ]
            });

        resourceBuilder.OnInitializeResource(async (resource, e, ct) =>
        {
            var notifications = e.Notifications;
            var loggerService = e.Services.GetRequiredService<ResourceLoggerService>();
            var logger = loggerService.GetLogger(resource);

            // Wait for the parent Kind cluster to be running before applying the manifest.
            await notifications.WaitForResourceAsync(resource.Parent.Name, KnownResourceStates.Running, ct);

            await e.Eventing.PublishAsync(new BeforeResourceStartedEvent(resource, e.Services), ct);

            await notifications.PublishUpdateAsync(resource,
                state => state with { State = KnownResourceStates.Starting });

            try
            {
                var processRunner = e.Services.GetRequiredService<IProcessRunner>();
                var kubectlManager = CreateKubectlManager(processRunner, resource);
                await kubectlManager.ApplyAsync(resource, logger, ct);

                await notifications.PublishUpdateAsync(resource,
                    state => state with
                    {
                        State = KnownResourceStates.Running,
                        Properties = [
                            new("ManifestPath", resource.ManifestPath),
                            new("Namespace", resource.Namespace ?? "(default)"),
                            new("ServerSide", resource.ServerSide.ToString()),
                            new("Mode", resource.IsKustomize ? "kustomize" : "apply"),
                        ]
                    });
            }
            catch (Exception)
            {
                await notifications.PublishUpdateAsync(resource,
                    state => state with { State = KnownResourceStates.FailedToStart });
                throw;
            }
        });

        return resourceBuilder;
    }

    internal static KubectlManager CreateKubectlManager(
        IProcessRunner processRunner,
        K8sManifestResource resource)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(resource);

        return new KubectlManager(
            processRunner,
            clusterInfoMaxWait: resource.ClusterReadyTimeout);
    }

    /// <summary>
    /// Recursively applies manifests from subdirectories (maps to <c>kubectl apply --recursive</c>).
    /// Only meaningful when the manifest path is a directory.
    /// </summary>
    /// <param name="builder">The manifest resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{K8sManifestResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<K8sManifestResource> WithRecursive(
        this IResourceBuilder<K8sManifestResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.Recursive = true;
        return builder;
    }

    /// <summary>
    /// Applies the manifest server-side (maps to <c>kubectl apply --server-side</c>).
    /// Required for large CRDs that exceed the client-side annotation size limit.
    /// </summary>
    /// <param name="builder">The manifest resource builder.</param>
    /// <param name="forceConflicts">
    /// When <see langword="true"/>, also passes <c>--force-conflicts</c> to override field ownership
    /// held by another field manager (e.g., a controller). Defaults to <see langword="false"/>.
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{K8sManifestResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<K8sManifestResource> WithServerSideApply(
        this IResourceBuilder<K8sManifestResource> builder,
        bool forceConflicts = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.ServerSide = true;
        builder.Resource.ForceConflicts = forceConflicts;
        return builder;
    }

    /// <summary>
    /// Sets the field manager name passed to <c>kubectl apply --field-manager</c>.
    /// </summary>
    /// <remarks>
    /// The flag is passed whenever this method is used, but it is primarily meaningful with
    /// server-side apply because Kubernetes records managed fields for server-side operations.
    /// </remarks>
    /// <param name="builder">The manifest resource builder.</param>
    /// <param name="fieldManager">The field manager name.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{K8sManifestResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<K8sManifestResource> WithFieldManager(
        this IResourceBuilder<K8sManifestResource> builder,
        string fieldManager)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(fieldManager);

        builder.Resource.FieldManager = fieldManager;
        return builder;
    }

    /// <summary>
    /// Sets the maximum time to wait for the Kubernetes API to become reachable
    /// before running <c>kubectl apply</c>.
    /// </summary>
    /// <param name="builder">The manifest resource builder.</param>
    /// <param name="timeout">The cluster readiness timeout.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{K8sManifestResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<K8sManifestResource> WithClusterReadyTimeout(
        this IResourceBuilder<K8sManifestResource> builder,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.ClusterReadyTimeout = KubectlTimeouts.Normalize(timeout, nameof(timeout));
        return builder;
    }

    /// <summary>
    /// Sets the maximum time to wait for <c>kubectl apply</c> to complete.
    /// </summary>
    /// <param name="builder">The manifest resource builder.</param>
    /// <param name="timeout">The apply timeout.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{K8sManifestResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<K8sManifestResource> WithApplyTimeout(
        this IResourceBuilder<K8sManifestResource> builder,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.ApplyTimeout = KubectlTimeouts.Normalize(timeout, nameof(timeout));
        return builder;
    }

    /// <summary>
    /// Sets the maximum time to wait for applied CRDs to reach the <c>Established</c> condition.
    /// </summary>
    /// <param name="builder">The manifest resource builder.</param>
    /// <param name="timeout">The CRD wait timeout.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{K8sManifestResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<K8sManifestResource> WithCrdWaitTimeout(
        this IResourceBuilder<K8sManifestResource> builder,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.CrdWaitTimeout = KubectlTimeouts.Normalize(timeout, nameof(timeout));
        return builder;
    }

    /// <summary>
    /// Sets whether CRD wait failures fail the manifest resource or are logged as best-effort warnings.
    /// </summary>
    /// <param name="builder">The manifest resource builder.</param>
    /// <param name="behavior">The CRD wait behavior.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{K8sManifestResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<K8sManifestResource> WithCrdWaitBehavior(
        this IResourceBuilder<K8sManifestResource> builder,
        CrdWaitBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.CrdWaitBehavior = behavior;
        return builder;
    }

}

#pragma warning restore ASPIREATS001