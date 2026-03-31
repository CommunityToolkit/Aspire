// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Kind;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Kind cluster resources to the distributed application builder.
/// </summary>
public static class KindClusterResourceBuilderExtensions
{
    /// <summary>
    /// Adds a Kind (Kubernetes in Docker) cluster resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the Kind cluster.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{KindClusterResource}"/>.</returns>
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
                    var manager = new KindClusterManager(resource, logger, processRunner);
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

            await EnsureKindCliIsAvailableAsync(processRunner, logger, ct);

            var manager = new KindClusterManager(resource, logger, processRunner);

            await notifications.PublishUpdateAsync(resource,
                state => state with { State = KnownResourceStates.Starting });

            await e.Eventing.PublishAsync(new BeforeResourceStartedEvent(resource, e.Services), ct);

            try
            {
                await manager.CreateClusterAsync(ct);
                await GenerateContainerKubeconfigAsync(resource, ct);

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
            catch (Exception)
            {
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
    /// <param name="builder">The resource builder.</param>
    /// <param name="version">The Kubernetes version (e.g., "v1.32.2").</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{KindClusterResource}"/>.</returns>
    public static IResourceBuilder<KindClusterResource> WithKubernetesVersion(
        this IResourceBuilder<KindClusterResource> builder,
        string version)
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
    /// <param name="builder">The resource builder.</param>
    /// <param name="count">The number of worker nodes. Must be zero or greater.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{KindClusterResource}"/>.</returns>
    public static IResourceBuilder<KindClusterResource> WithWorkerNodes(
        this IResourceBuilder<KindClusterResource> builder,
        int count)
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
    /// <param name="builder">The resource builder.</param>
    /// <param name="lifetime">The desired cluster lifetime.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{KindClusterResource}"/>.</returns>
    public static IResourceBuilder<KindClusterResource> WithClusterLifetime(
        this IResourceBuilder<KindClusterResource> builder,
        ClusterLifetime lifetime)
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
    public static IResourceBuilder<KindClusterResource> WithKindConfig(
        this IResourceBuilder<KindClusterResource> builder,
        Action<KindConfigModel> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.WithAnnotation(new KindConfigAnnotation(configure));

        return builder;
    }

    /// <summary>
    /// Configures a resource to reference the Kind cluster by injecting kubeconfig environment variables.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="kind">The Kind cluster resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
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

    private static KindNodeImageAnnotation GetOrCreateNodeImageAnnotation(KindClusterResource resource)
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
        catch (Win32Exception)
        {
            throw new InvalidOperationException(
                "Kind CLI not found. Install it from https://kind.sigs.k8s.io/docs/user/quick-start/#installation");
        }
    }

    /// <summary>
    /// Generates a modified kubeconfig file suitable for container-to-container access
    /// over the Kind Docker network.
    /// </summary>
    /// <remarks>
    /// Kind clusters bind the API server to 127.0.0.1, which is unreachable from other
    /// Docker containers. This method creates a copy of the kubeconfig that:
    /// 1. Replaces 127.0.0.1:{port} with the control-plane container name on port 6443.
    /// 2. Disables TLS verification (the cert is issued for 127.0.0.1, not the container name).
    /// </remarks>
    private static async Task GenerateContainerKubeconfigAsync(KindClusterResource resource, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(resource.KubeconfigPath, ct).ConfigureAwait(false);

        var controlPlane = $"{resource.Name}-control-plane";

        content = System.Text.RegularExpressions.Regex.Replace(
            content,
            @"https://127\.0\.0\.1:\d+",
            $"https://{controlPlane}:6443");

        content = content.Replace(
            "certificate-authority-data:",
            "insecure-skip-tls-verify: true\n    #certificate-authority-data:");

        Directory.CreateDirectory(Path.GetDirectoryName(resource.ContainerKubeconfigPath)!);
        await File.WriteAllTextAsync(resource.ContainerKubeconfigPath, content, ct).ConfigureAwait(false);
    }

}
