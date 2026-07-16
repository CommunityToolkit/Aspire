// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Annotation that stores the <see cref="ClusterLifetime"/> for a Kind cluster resource.
/// </summary>
internal sealed class ClusterLifetimeAnnotation : IResourceAnnotation
{
    /// <summary>
    /// Gets or sets the cluster lifetime.
    /// </summary>
    public required ClusterLifetime Lifetime { get; set; }
}
