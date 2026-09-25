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
    /// Sets the maximum time for the post-apply check to wait for CRDs
    /// to reach the <c>Established</c> condition before Running.
    /// </summary>
    /// <typeparam name="T">The deployed resource type.</typeparam>
    /// <param name="builder">The Kind deployment resource builder.</param>
    /// <param name="timeout">The CRD wait timeout.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithCrdWaitTimeout<T>(
        this IResourceBuilder<T> builder,
        TimeSpan timeout)
        where T : KindDeployedResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        KindCrdWaitPolicies.GetOrCreate(builder.Resource).Timeout = KubectlTimeouts.Normalize(timeout, nameof(timeout));
        return builder;
    }

    /// <summary>
    /// Sets whether CRD establishment failures prevent Running or are logged as
    /// best-effort warnings while allowing the Kind deployment to run unverified.
    /// </summary>
    /// <typeparam name="T">The deployed resource type.</typeparam>
    /// <param name="builder">The Kind deployment resource builder.</param>
    /// <param name="behavior">The CRD wait behavior.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithCrdWaitBehavior<T>(
        this IResourceBuilder<T> builder,
        CrdWaitBehavior behavior)
        where T : KindDeployedResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        KindCrdWaitPolicies.GetOrCreate(builder.Resource).FailureBehavior = behavior;
        return builder;
    }
}

#pragma warning restore ASPIREATS001
