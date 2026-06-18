// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Base class for resources deployed to a Kind cluster.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="parent">The parent Kind cluster resource.</param>
public abstract class KindDeployedResource(string name, KindClusterResource parent)
    : Resource(name),
      IResourceWithParent<KindClusterResource>,
      IResourceWithWaitSupport
{
    /// <summary>
    /// Gets the parent Kind cluster resource.
    /// </summary>
    public KindClusterResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));

    /// <summary>
    /// Gets or sets the Kubernetes namespace for the deployment.
    /// When <see langword="null"/>, the Helm default namespace is used.
    /// </summary>
    public string? Namespace { get; set; }
}
