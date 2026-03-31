// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Represents an annotation that customizes the Kind cluster configuration.
/// Multiple annotations compose: all are applied in order during config generation.
/// </summary>
internal sealed class KindConfigAnnotation : IResourceAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KindConfigAnnotation"/> class.
    /// </summary>
    /// <param name="configure">The callback to configure the Kind config model.</param>
    public KindConfigAnnotation(Action<KindConfigModel> configure)
    {
        Configure = configure;
    }

    /// <summary>
    /// Gets the configuration callback.
    /// </summary>
    public Action<KindConfigModel> Configure { get; }
}

/// <summary>
/// Represents an annotation that controls the Kind node image for every node
/// in the cluster.
/// </summary>
internal sealed class KindNodeImageAnnotation : IResourceAnnotation
{
    /// <summary>
    /// Gets or sets the Kubernetes version (e.g., "v1.32.2").
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the container image repository (e.g., "myacr.azurecr.io/kindest/node").
    /// Defaults to "kindest/node".
    /// </summary>
    public string Registry { get; set; } = "kindest/node";
}

/// <summary>
/// Represents an annotation that sets the number of worker nodes in the Kind cluster.
/// </summary>
internal sealed class WorkerNodesAnnotation : IResourceAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerNodesAnnotation"/> class.
    /// </summary>
    /// <param name="count">The desired number of worker nodes.</param>
    public WorkerNodesAnnotation(int count)
    {
        Count = count;
    }

    /// <summary>
    /// Gets the desired number of worker nodes.
    /// </summary>
    public int Count { get; }
}
