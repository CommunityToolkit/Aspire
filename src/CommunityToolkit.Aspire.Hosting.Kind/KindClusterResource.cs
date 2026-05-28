// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a resource that manages a local Kind cluster for development.
/// </summary>
public sealed class KindClusterResource : Resource, IKindResource, IResourceWithWaitSupport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KindClusterResource"/> class.
    /// </summary>
    /// <param name="name">The name of the cluster.</param>
    public KindClusterResource(string name)
        : base(name)
    {
        KubeconfigPath = Path.Combine(Path.GetTempPath(), "aspire-kind", name, "kubeconfig.yaml");
        ContainerKubeconfigPath = Path.Combine(Path.GetTempPath(), "aspire-kind", name, "container-kubeconfig.yaml");
    }

    /// <summary>
    /// Gets the path to the kubeconfig file for this Kind cluster.
    /// </summary>
    public string KubeconfigPath { get; }

    /// <summary>
    /// Gets the path to the container-compatible kubeconfig file.
    /// This kubeconfig uses the Kind control-plane container name instead of 127.0.0.1,
    /// enabling container-to-container access over the Kind container network.
    /// </summary>
    public string ContainerKubeconfigPath { get; }

}
