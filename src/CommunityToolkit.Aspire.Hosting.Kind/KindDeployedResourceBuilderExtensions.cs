// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Kind;

#pragma warning disable ASPIREATS001 // AspireExport APIs are experimental

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for resources deployed to Kind clusters.
/// </summary>
public static class KindDeployedResourceBuilderExtensions
{
    /// <summary>
    /// Configures how the post-apply check waits for CRDs to reach the
    /// <c>Established</c> condition before the resource becomes running.
    /// </summary>
    /// <typeparam name="T">The deployed resource type.</typeparam>
    /// <param name="builder">The Kind deployment resource builder.</param>
    /// <param name="configure">The callback used to configure the CRD wait options.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// Repeated calls start from the current policy and preserve options that the callback does not change.
    /// Timeout assignments are validated immediately. Changes are applied only after the callback completes.
    /// </remarks>
    [AspireExport(RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<T> WithCrdWait<T>(
        this IResourceBuilder<T> builder,
        Action<CrdWaitOptions> configure)
        where T : KindDeployedResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var policy);
        var options = new CrdWaitOptions
        {
            Timeout = policy?.Options.Timeout ?? KubectlTimeouts.DefaultCrdWaitTimeout,
            FailureBehavior = policy?.Options.FailureBehavior ?? CrdWaitBehavior.Fail,
        };
        configure(options);

        if (policy is null)
        {
            builder.Resource.Annotations.Add(new KindCrdWaitPolicyAnnotation(options));
        }
        else
        {
            policy.Options = options;
        }
        return builder;
    }
}

#pragma warning restore ASPIREATS001
