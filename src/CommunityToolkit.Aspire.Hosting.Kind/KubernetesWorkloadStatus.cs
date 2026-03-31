// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Represents the replica counts for a single Kubernetes object (Deployment or StatefulSet).
/// </summary>
internal readonly record struct KubernetesObjectStatus(
    string Kind,
    string Name,
    int DesiredReplicas,
    int ReadyReplicas,
    int AvailableReplicas,
    int UpdatedReplicas);

/// <summary>
/// The result of querying Kubernetes for workload statuses.
/// </summary>
internal readonly record struct KubernetesWorkloadStatus(
    IReadOnlyList<KubernetesObjectStatus> Workloads);

/// <summary>
/// Evaluates readiness of Kubernetes workloads based on replica counts.
/// </summary>
internal static class WorkloadReadiness
{
    /// <summary>
    /// Determines whether a single Kubernetes object has reached its desired replica count.
    /// </summary>
    public static bool IsReady(this KubernetesObjectStatus status) => status.Kind switch
    {
        "Deployment" => status.ReadyReplicas >= status.DesiredReplicas
            && status.AvailableReplicas >= status.DesiredReplicas
            && status.UpdatedReplicas >= status.DesiredReplicas,
        "StatefulSet" => status.ReadyReplicas >= status.DesiredReplicas,
        _ => false,
    };
}
