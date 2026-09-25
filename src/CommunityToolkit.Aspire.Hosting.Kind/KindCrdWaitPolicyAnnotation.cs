// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Kind;

internal sealed class KindCrdWaitPolicyAnnotation : IResourceAnnotation
{
    public TimeSpan Timeout { get; set; } = KubectlTimeouts.DefaultCrdWaitTimeout;

    public CrdWaitBehavior FailureBehavior { get; set; } = CrdWaitBehavior.Fail;
}

internal static class KindCrdWaitPolicies
{
    public static KindCrdWaitPolicyAnnotation GetOrCreate(KindDeployedResource resource)
    {
        if (resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var annotation))
        {
            return annotation;
        }

        annotation = new KindCrdWaitPolicyAnnotation();
        resource.Annotations.Add(annotation);
        return annotation;
    }
}
