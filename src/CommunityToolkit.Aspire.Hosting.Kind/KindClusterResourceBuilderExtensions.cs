// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Kind;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREATS001 // AspireExport APIs are experimental

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Kind cluster resources to the distributed application builder.
/// </summary>
public static class KindClusterResourceBuilderExtensions
{
    /// <summary>
    /// Adds a Kind cluster resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the Kind cluster.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{KindClusterResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<KindClusterResource> AddKindCluster(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var resource = new KindClusterResource(name);

        builder.Services.AddKindInfrastructure();
        builder.Services.TryAddEventingSubscriber<KindClusterLifecycleHook>();

        var healthCheckKey = $"kind_{name}";
        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                healthCheckKey,
                sp =>
                {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<KindClusterManager>();
                    var processRunner = sp.GetRequiredService<IProcessRunner>();
                    var containerRuntimeResolver = sp.GetRequiredService<IKindContainerRuntimeResolver>();
                    var manager = new KindClusterManager(resource, logger, processRunner, containerRuntimeResolver);
                    return new KindHealthCheck(manager);
                },
                failureStatus: null,
                tags: null,
                timeout: null));

        var resourceBuilder = builder
            .AddResource(resource)
            .ExcludeFromManifest()
            .WithHealthCheck(healthCheckKey)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Kind Cluster",
                State = KnownResourceStates.NotStarted,
                Properties = [
                    new("ClusterName", resource.Name),
                ]
            });

        resourceBuilder.OnInitializeResource(async (resource, e, ct) =>
        {
            var notifications = e.Notifications;
            var loggerService = e.Services.GetRequiredService<ResourceLoggerService>();
            var logger = loggerService.GetLogger(resource);
            var processRunner = e.Services.GetRequiredService<IProcessRunner>();
            var containerRuntimeResolver = e.Services.GetRequiredService<IKindContainerRuntimeResolver>();

            await EnsureKindCliIsAvailableAsync(processRunner, logger, ct);

            var manager = new KindClusterManager(resource, logger, processRunner, containerRuntimeResolver);

            await notifications.PublishUpdateAsync(resource,
                state => state with { State = KnownResourceStates.Starting });

            await e.Eventing.PublishAsync(new BeforeResourceStartedEvent(resource, e.Services), ct);

            try
            {
                await manager.CreateClusterAsync(ct);
                await KindContainerHelper.GenerateContainerKubeconfigAsync(resource, ct);

                var lifetime = resource.TryGetLastAnnotation<ClusterLifetimeAnnotation>(out var lifetimeAnnotation)
                    ? lifetimeAnnotation.Lifetime
                    : ClusterLifetime.Session;

                await notifications.PublishUpdateAsync(resource,
                    state => state with
                    {
                        State = KnownResourceStates.Running,
                        Properties = [
                            new("ClusterName", resource.Name),
                            new("KubeConfigPath", resource.KubeconfigPath),
                            new("Lifetime", lifetime.ToString()),
                        ]
                    });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Kind cluster '{ClusterName}' failed to start.", resource.Name);
                await notifications.PublishUpdateAsync(resource,
                    state => state with { State = KnownResourceStates.FailedToStart });
                throw;
            }
        });

        return resourceBuilder;
    }

    /// <summary>
    /// Sets the Kubernetes version for the Kind cluster.
    /// </summary>
    /// <typeparam name="T">A resource type implementing <see cref="IKindResource"/>.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="version">The Kubernetes version (e.g., "v1.32.2").</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithKubernetesVersion<T>(
        this IResourceBuilder<T> builder,
        string version)
        where T : IKindResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(version);

        var annotation = GetOrCreateNodeImageAnnotation(builder.Resource);
        annotation.Version = version;
        return builder;
    }

    /// <summary>
    /// Sets the number of worker nodes for the Kind cluster.
    /// </summary>
    /// <typeparam name="T">A resource type implementing <see cref="IKindResource"/>.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="count">The number of worker nodes. Must be zero or greater.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithWorkerNodes<T>(
        this IResourceBuilder<T> builder,
        int count)
        where T : IKindResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return builder.WithAnnotation(new WorkerNodesAnnotation(count), ResourceAnnotationMutationBehavior.Replace);
    }

    /// <summary>
    /// Sets the cluster lifetime. When <see cref="ClusterLifetime.Session"/> (the default),
    /// the cluster is deleted when the AppHost shuts down. When <see cref="ClusterLifetime.Persistent"/>,
    /// the cluster survives AppHost restarts and is reused on next startup.
    /// </summary>
    /// <typeparam name="T">A resource type implementing <see cref="IKindResource"/>.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="lifetime">The desired cluster lifetime.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithClusterLifetime<T>(
        this IResourceBuilder<T> builder,
        ClusterLifetime lifetime)
        where T : IKindResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithAnnotation(new ClusterLifetimeAnnotation { Lifetime = lifetime }, ResourceAnnotationMutationBehavior.Replace);
    }

    /// <summary>
    /// Customizes the Kind cluster configuration. The callback receives the <see cref="KindConfigModel"/>
    /// after default nodes have been populated, allowing arbitrary modifications before serialization to YAML.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configure">A callback to customize the Kind configuration model.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{KindClusterResource}"/>.</returns>
    /// <remarks>
    /// Multiple calls to this method compose: each callback is applied in order during config generation.
    /// </remarks>
    [AspireExportIgnore(Reason = "Action<KindConfigModel> is not ATS-compatible. Configure Kind config through ATS-supported APIs.")]
    public static IResourceBuilder<KindClusterResource> WithKindConfig(
        this IResourceBuilder<KindClusterResource> builder,
        Action<KindConfigModel> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.WithAnnotation(new KindConfigAnnotation(configure));

        return builder;
    }

    private static KindNodeImageAnnotation GetOrCreateNodeImageAnnotation(IResource resource)
    {
        if (resource.TryGetLastAnnotation<KindNodeImageAnnotation>(out var existing))
        {
            return existing;
        }

        var annotation = new KindNodeImageAnnotation();
        resource.Annotations.Add(annotation);
        return annotation;
    }

    /// <summary>
    /// Verifies that the Kind CLI is installed and available on PATH.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the Kind CLI is not found.</exception>
    private static async Task EnsureKindCliIsAvailableAsync(
        IProcessRunner processRunner,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await processRunner.RunAsync(logger, "kind", ["version"], cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Kind CLI not found. Install it from https://kind.sigs.k8s.io/docs/user/quick-start/#installation");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Kind CLI not found. Install it from https://kind.sigs.k8s.io/docs/user/quick-start/#installation", ex);
        }
    }

    /// <summary>
    /// Configures a non-container resource to reference the Kind cluster by injecting kubeconfig environment variables.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="kind">The Kind cluster resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// This overload sets <c>KUBECONFIG</c> to the host kubeconfig path, which is appropriate for 
    /// resources that execute on the host (e.g., executables, projects).
    /// </para>
    /// <para>
    /// For container resources, use the <see cref="KindContainerExtensions.WithReference"/> 
    /// overload instead, which handles container-specific requirements like bind-mounting and network connectivity.
    /// </para>
    /// </remarks>
    [AspireExport("withKindClusterReference")]
    public static IResourceBuilder<T> WithReference<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<KindClusterResource> kind)
        where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(kind);

        return builder.WithEnvironment(context =>
        {
            context.EnvironmentVariables["KUBECONFIG"] = kind.Resource.KubeconfigPath;
            context.EnvironmentVariables["K8S_CLUSTER_NAME"] = kind.Resource.Name;
        });
    }
}

#pragma warning restore ASPIREATS001
