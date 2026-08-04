// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a resource that manages or targets a Kind cluster.
/// </summary>
public interface IKindResource : IResource
{
    /// <summary>
    /// Gets the path to the kubeconfig file for this Kind cluster.
    /// </summary>
    string KubeconfigPath { get; }
}
