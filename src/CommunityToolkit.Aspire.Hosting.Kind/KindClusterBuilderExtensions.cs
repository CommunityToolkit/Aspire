using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aspire.Hosting;

/// <summary>
/// Provides fluent extension methods for adding and configuring Kind cluster resources
/// in a .NET Aspire <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class KindClusterBuilderExtensions
{
    /// <summary>
    /// Adds a Kind (Kubernetes in Docker) cluster resource to the distributed application.
    /// The cluster is created before the application starts and deleted when it stops.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">
    /// The Aspire resource name. Also used as the Kind cluster name when
    /// <paramref name="clusterName"/> is not specified.
    /// </param>
    /// <param name="clusterName">
    /// The Kind cluster name passed to <c>kind create/delete cluster --name</c>.
    /// Defaults to <paramref name="name"/> when not specified.
    /// Must match <c>^[a-z0-9][a-z0-9\-]*$</c>.
    /// </param>
    /// <param name="kubeconfigPath">
    /// Absolute path where the kubeconfig file will be written by Kind.
    /// Defaults to a file in <see cref="Path.GetTempPath"/> when not specified.
    /// </param>
    /// <returns>A builder for the <see cref="KindClusterResource"/> that supports further configuration.</returns>
    /// <example>
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var cluster = builder
    ///     .AddKindCluster("dev-cluster")
    ///     .WithNodeCount(2)
    ///     .WithKubernetesVersion("v1.31.0");
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    public static IResourceBuilder<KindClusterResource> AddKindCluster(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string? clusterName = null,
        string? kubeconfigPath = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var resolvedClusterName = clusterName ?? name;
        var resolvedKubeconfigPath = kubeconfigPath
            ?? Path.Combine(Path.GetTempPath(), $"kind-{resolvedClusterName}-kubeconfig.yaml");

        var resource = new KindClusterResource(name, resolvedClusterName, resolvedKubeconfigPath);

        // TryAddEnumerable is idempotent: if AddKindCluster is called multiple times, only
        // one KindClusterLifecycleHook singleton is registered.
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IDistributedApplicationLifecycleHook, KindClusterLifecycleHook>());

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Sets the number of additional worker nodes in the cluster.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="nodeCount">Number of worker nodes (must be ≥ 0).</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IResourceBuilder<KindClusterResource> WithNodeCount(
        this IResourceBuilder<KindClusterResource> builder,
        int nodeCount)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (nodeCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeCount), nodeCount, "Node count must be non-negative.");
        }

        builder.Resource.NodeCount = nodeCount;
        return builder;
    }

    /// <summary>
    /// Sets the Kubernetes version for all cluster nodes (e.g., <c>"v1.31.0"</c>).
    /// Kind will use the <c>kindest/node:{version}</c> image.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="kubernetesVersion">
    /// The Kubernetes version tag (including the leading <c>v</c>), or <see langword="null"/> to use
    /// the default version bundled with Kind.
    /// </param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IResourceBuilder<KindClusterResource> WithKubernetesVersion(
        this IResourceBuilder<KindClusterResource> builder,
        string? kubernetesVersion)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.KubernetesVersion = kubernetesVersion;
        return builder;
    }

    /// <summary>
    /// Provides a custom Kind cluster configuration file to pass to
    /// <c>kind create cluster --config</c>.
    /// Takes precedence over any auto-generated configuration.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configPath">Absolute path to the Kind cluster config YAML file.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IResourceBuilder<KindClusterResource> WithConfig(
        this IResourceBuilder<KindClusterResource> builder,
        string configPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(configPath);

        builder.Resource.ConfigPath = configPath;
        return builder;
    }

    /// <summary>
    /// Adds a host-to-container port mapping on the Kind control-plane node.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="hostPort">The port on the Docker host.</param>
    /// <param name="containerPort">The port inside the Kind node container.</param>
    /// <param name="protocol">Network protocol — <c>"TCP"</c> or <c>"UDP"</c>. Defaults to <c>"TCP"</c>.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IResourceBuilder<KindClusterResource> WithPortMapping(
        this IResourceBuilder<KindClusterResource> builder,
        int hostPort,
        int containerPort,
        string protocol = "TCP")
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.AddPortMapping(new KindPortMapping(hostPort, containerPort, protocol));
        return builder;
    }

    /// <summary>
    /// Installs a Helm chart into the cluster after it becomes healthy.
    /// Requires <c>helm</c> to be installed and available on <c>PATH</c>.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="releaseName">The Helm release name.</param>
    /// <param name="chart">
    /// The chart reference, e.g. <c>"ingress-nginx/ingress-nginx"</c> or a local path.
    /// </param>
    /// <param name="namespace">
    /// The Kubernetes namespace for the release. Defaults to <c>"default"</c>.
    /// The namespace is created automatically if it does not exist.
    /// </param>
    /// <param name="valuesFile">Optional path to a Helm values override file.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IResourceBuilder<KindClusterResource> WithHelmChart(
        this IResourceBuilder<KindClusterResource> builder,
        string releaseName,
        string chart,
        string @namespace = "default",
        string? valuesFile = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(releaseName);
        ArgumentException.ThrowIfNullOrEmpty(chart);

        builder.Resource.AddHelmChart(new KindHelmChart(releaseName, chart, @namespace, valuesFile));
        return builder;
    }

    /// <summary>
    /// Applies a kubectl manifest file to the cluster after it becomes healthy.
    /// Requires <c>kubectl</c> to be installed and available on <c>PATH</c>.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="manifestPath">Absolute path to the Kubernetes manifest file or directory.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IResourceBuilder<KindClusterResource> WithManifest(
        this IResourceBuilder<KindClusterResource> builder,
        string manifestPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(manifestPath);

        builder.Resource.AddManifestPath(manifestPath);
        return builder;
    }

    /// <summary>
    /// Overrides the default 5-minute timeout for waiting until the cluster becomes healthy.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="timeout">The maximum time to wait (must be positive).</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IResourceBuilder<KindClusterResource> WithWaitForReady(
        this IResourceBuilder<KindClusterResource> builder,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout), timeout, "Ready timeout must be a positive duration.");
        }

        builder.Resource.ReadyTimeout = timeout;
        return builder;
    }
}
