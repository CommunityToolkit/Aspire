// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Contains constants for the Kind node container image tags.
/// </summary>
public static class KindContainerImageTags
{
    /// <summary>
    /// The Kind node image repository used for cluster nodes.
    /// </summary>
    public const string KindNodeImageRepository = "kindest/node";

    /// <summary>
    /// The default Kubernetes version for Kind clusters.
    /// </summary>
    public const string DefaultKubernetesVersion = "v1.32.0";
}
