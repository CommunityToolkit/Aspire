// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Kind;

internal sealed class KindCrdWaitPolicyAnnotation : IResourceAnnotation
{
    public KindCrdWaitPolicyAnnotation(CrdWaitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
    }

    public CrdWaitOptions Options { get; set; }
}

internal static class KindCrdWaitPolicies
{
    public static KindCrdWaitPolicyAnnotation GetOrCreate(KindDeployedResource resource)
    {
        if (resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var annotation))
        {
            return annotation;
        }

        annotation = new KindCrdWaitPolicyAnnotation(new CrdWaitOptions());
        resource.Annotations.Add(annotation);
        return annotation;
    }
}
