// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Kind;

internal sealed class K8sManifestApplyOptionsAnnotation : IResourceAnnotation
{
    public bool IsKustomize { get; set; }

    public bool Recursive { get; set; }

    public bool ServerSide { get; set; }

    public bool ForceConflicts { get; set; }

    public string? FieldManager { get; set; }

    public TimeSpan ApplyTimeout { get; set; } = KubectlTimeouts.DefaultApplyTimeout;
}

internal sealed class K8sManifestCrdWaitPolicy
{
    public TimeSpan Timeout { get; set; } = KubectlTimeouts.DefaultCrdWaitTimeout;

    public CrdWaitBehavior FailureBehavior { get; set; } = CrdWaitBehavior.Fail;
}

internal sealed class K8sManifestWaitPolicyAnnotation : IResourceAnnotation
{
    public TimeSpan ClusterReadyTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public K8sManifestCrdWaitPolicy Crd { get; } = new();
}

internal static class K8sManifestAnnotations
{
    public static K8sManifestApplyOptionsAnnotation GetApplyOptions(K8sManifestResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return resource.TryGetLastAnnotation<K8sManifestApplyOptionsAnnotation>(out var annotation)
            ? annotation
            : new K8sManifestApplyOptionsAnnotation();
    }

    public static K8sManifestApplyOptionsAnnotation GetOrCreateApplyOptions(K8sManifestResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.TryGetLastAnnotation<K8sManifestApplyOptionsAnnotation>(out var annotation))
        {
            return annotation;
        }

        annotation = new K8sManifestApplyOptionsAnnotation();
        resource.Annotations.Add(annotation);
        return annotation;
    }

    public static K8sManifestWaitPolicyAnnotation GetWaitPolicy(K8sManifestResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return resource.TryGetLastAnnotation<K8sManifestWaitPolicyAnnotation>(out var annotation)
            ? annotation
            : new K8sManifestWaitPolicyAnnotation();
    }

    public static K8sManifestWaitPolicyAnnotation GetOrCreateWaitPolicy(K8sManifestResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.TryGetLastAnnotation<K8sManifestWaitPolicyAnnotation>(out var annotation))
        {
            return annotation;
        }

        annotation = new K8sManifestWaitPolicyAnnotation();
        resource.Annotations.Add(annotation);
        return annotation;
    }
}
