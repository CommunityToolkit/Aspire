// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Kubernetes;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A surrogate resource that provides Kind cluster configuration for a
/// <see cref="KubernetesEnvironmentResource"/>. Created by the
/// <c>WithKind()</c> extension method.
/// </summary>
public sealed class KindEnvironmentResource : Resource, IKindResource, IResourceWithParent<KubernetesEnvironmentResource>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KindEnvironmentResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="parent">The parent Kubernetes environment resource.</param>
    public KindEnvironmentResource(string name, KubernetesEnvironmentResource parent)
        : base(name)
    {
        Parent = parent;
        KubeconfigPath = Path.Combine(Path.GetTempPath(), "aspire-kind", name, "kubeconfig.yaml");
    }

    /// <summary>
    /// Gets the parent <see cref="KubernetesEnvironmentResource"/>.
    /// </summary>
    public KubernetesEnvironmentResource Parent { get; }

    /// <inheritdoc />
    public string KubeconfigPath { get; }
}
