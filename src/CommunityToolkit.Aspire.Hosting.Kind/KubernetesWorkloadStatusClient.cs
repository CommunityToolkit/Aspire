// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Queries Kubernetes workload readiness using the official C# Kubernetes client.
/// </summary>
internal static class KubernetesWorkloadStatusClient
{
    /// <summary>
    /// Queries Deployments and StatefulSets matching the given label selector.
    /// </summary>
    /// <param name="client">An <see cref="IKubernetes"/> client configured for the target cluster.</param>
    /// <param name="labelSelector">Kubernetes label selector (e.g. <c>app.kubernetes.io/instance=redis</c>).</param>
    /// <param name="namespace">
    /// Namespace to query, or <see langword="null"/> to query all namespaces.
    /// </param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<KubernetesWorkloadStatus> GetWorkloadStatusAsync(
        IKubernetes client,
        string labelSelector,
        string? @namespace,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Querying workloads with selector '{Selector}' in namespace '{Namespace}'",
            labelSelector,
            @namespace ?? "(all)");

        List<KubernetesObjectStatus> workloads = [];

        if (!string.IsNullOrEmpty(@namespace))
        {
            V1DeploymentList deployments = await client.AppsV1.ListNamespacedDeploymentAsync(
                @namespace,
                labelSelector: labelSelector,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            V1StatefulSetList statefulSets = await client.AppsV1.ListNamespacedStatefulSetAsync(
                @namespace,
                labelSelector: labelSelector,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            workloads.AddRange(deployments.Items.Select(ToWorkloadStatus));
            workloads.AddRange(statefulSets.Items.Select(ToWorkloadStatus));
        }
        else
        {
            V1DeploymentList deployments = await client.AppsV1.ListDeploymentForAllNamespacesAsync(
                labelSelector: labelSelector,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            V1StatefulSetList statefulSets = await client.AppsV1.ListStatefulSetForAllNamespacesAsync(
                labelSelector: labelSelector,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            workloads.AddRange(deployments.Items.Select(ToWorkloadStatus));
            workloads.AddRange(statefulSets.Items.Select(ToWorkloadStatus));
        }

        return new KubernetesWorkloadStatus(workloads);
    }

    /// <summary>
    /// Projects a <see cref="V1Deployment"/> into a <see cref="KubernetesObjectStatus"/>.
    /// </summary>
    internal static KubernetesObjectStatus ToWorkloadStatus(V1Deployment deployment) =>
        new(
            Kind: "Deployment",
            Name: deployment.Metadata?.Name ?? "unknown",
            DesiredReplicas: deployment.Spec?.Replicas ?? 1,
            ReadyReplicas: deployment.Status?.ReadyReplicas ?? 0,
            AvailableReplicas: deployment.Status?.AvailableReplicas ?? 0,
            UpdatedReplicas: deployment.Status?.UpdatedReplicas ?? 0);

    /// <summary>
    /// Projects a <see cref="V1StatefulSet"/> into a <see cref="KubernetesObjectStatus"/>.
    /// </summary>
    internal static KubernetesObjectStatus ToWorkloadStatus(V1StatefulSet statefulSet) =>
        new(
            Kind: "StatefulSet",
            Name: statefulSet.Metadata?.Name ?? "unknown",
            DesiredReplicas: statefulSet.Spec?.Replicas ?? 1,
            ReadyReplicas: statefulSet.Status?.ReadyReplicas ?? 0,
            AvailableReplicas: 0,
            UpdatedReplicas: statefulSet.Status?.UpdatedReplicas ?? 0);
}
