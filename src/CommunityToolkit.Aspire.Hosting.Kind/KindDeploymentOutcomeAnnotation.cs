// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Kind;

internal sealed class KindDeploymentOutcomeAnnotation : IResourceAnnotation
{
    public IReadOnlyList<string>? CrdNames { get; set; }
}

internal static class KindDeploymentOutcomes
{
    public static KindDeploymentOutcomeAnnotation GetOrCreate(KindDeployedResource resource)
    {
        if (resource.TryGetLastAnnotation<KindDeploymentOutcomeAnnotation>(out var annotation))
        {
            return annotation;
        }

        annotation = new KindDeploymentOutcomeAnnotation();
        resource.Annotations.Add(annotation);
        return annotation;
    }
}
