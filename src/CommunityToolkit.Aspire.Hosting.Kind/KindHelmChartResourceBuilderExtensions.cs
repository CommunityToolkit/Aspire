// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Kind;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREATS001 // AspireExport APIs are experimental

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Helm chart resources to Kind clusters.
/// </summary>
public static class KindHelmChartResourceBuilderExtensions
{
    /// <summary>
    /// Adds a Helm chart to be deployed to the Kind cluster.
    /// </summary>
    /// <param name="builder">The Kind cluster resource builder.</param>
    /// <param name="name">The name of the Helm release.</param>
    /// <param name="chartRef">
    /// Chart reference passed to <c>helm install</c>. Any form Helm supports:
    /// OCI (<c>oci://registry/chart</c>), repo (<c>repo/chart</c>),
    /// or local path (<c>./charts/my-app</c>).
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{KindHelmChartResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<KindHelmChartResource> AddHelmChart(
        this IResourceBuilder<KindClusterResource> builder,
        [ResourceName] string name,
        string chartRef)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(chartRef);

        var resource = new KindHelmChartResource(name, chartRef, builder.Resource);

        var healthCheckKey = $"helm_{name}";
        builder.ApplicationBuilder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                healthCheckKey,
                sp =>
                {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<KubernetesWorkloadHealthCheck>();
                    return new KubernetesWorkloadHealthCheck(
                        resource.Parent.KubeconfigPath,
                        $"app.kubernetes.io/instance={resource.ReleaseName}",
                        resource.Namespace,
                        logger);
                },
                failureStatus: null,
                tags: null,
                timeout: null));

        var resourceBuilder = builder.ApplicationBuilder
            .AddResource(resource)
            .ExcludeFromManifest()
            .WithHealthCheck(healthCheckKey)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Helm Chart",
                State = KnownResourceStates.NotStarted,
                Properties = [
                    new("ReleaseName", resource.ReleaseName),
                    new("ChartRef", resource.ChartRef),
                ]
            });

        resourceBuilder.OnInitializeResource(async (resource, e, ct) =>
        {
            var notifications = e.Notifications;
            var loggerService = e.Services.GetRequiredService<ResourceLoggerService>();
            var logger = loggerService.GetLogger(resource);

            // Wait for the parent Kind cluster to be running before installing the chart.
            await notifications.WaitForResourceAsync(resource.Parent.Name, KnownResourceStates.Running, ct);

            await e.Eventing.PublishAsync(new BeforeResourceStartedEvent(resource, e.Services), ct);
            
            await notifications.PublishUpdateAsync(resource,
                state => state with { State = KnownResourceStates.Starting });

            try
            {
                var processRunner = e.Services.GetRequiredService<IProcessRunner>();
                var helmManager = new HelmManager(processRunner);
                await helmManager.InstallAsync(resource, logger, ct);

                await notifications.PublishUpdateAsync(resource,
                    state => state with
                    {
                        State = KnownResourceStates.Running,
                        Properties = [
                            new("ReleaseName", resource.ReleaseName),
                            new("ChartRef", resource.ChartRef),
                            new("ChartVersion", resource.Version ?? "latest"),
                            new("Namespace", resource.Namespace ?? "default"),
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
    /// Sets the chart version (maps to <c>helm install --version</c>).
    /// </summary>
    /// <param name="builder">The Helm chart resource builder.</param>
    /// <param name="version">The chart version.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{KindHelmChartResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<KindHelmChartResource> WithChartVersion(
        this IResourceBuilder<KindHelmChartResource> builder,
        string version)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(version);

        builder.Resource.Version = version;
        return builder;
    }

    /// <summary>
    /// Sets a Helm value (maps to <c>helm install --set key=value</c>).
    /// </summary>
    /// <param name="builder">The Helm chart resource builder.</param>
    /// <param name="key">The Helm value key.</param>
    /// <param name="value">The Helm value.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{KindHelmChartResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<KindHelmChartResource> WithHelmValue(
        this IResourceBuilder<KindHelmChartResource> builder,
        string key,
        string value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        builder.Resource.Values[key] = value;
        return builder;
    }

    /// <summary>
    /// Adds a values file (maps to <c>helm install -f path</c>).
    /// </summary>
    /// <param name="builder">The Helm chart resource builder.</param>
    /// <param name="path">The path to the values file.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{KindHelmChartResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<KindHelmChartResource> WithHelmValuesFile(
        this IResourceBuilder<KindHelmChartResource> builder,
        string path)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(path);

        builder.Resource.ValuesFiles.Add(path);
        return builder;
    }

    /// <summary>
    /// Sets the Kubernetes namespace for the deployment.
    /// </summary>
    /// <typeparam name="T">The deployed resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="namespace">The Kubernetes namespace.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithNamespace<T>(
        this IResourceBuilder<T> builder,
        string @namespace)
        where T : KindDeployedResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(@namespace);

        builder.Resource.Namespace = @namespace;
        return builder;
    }
}

#pragma warning restore ASPIREATS001
