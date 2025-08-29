// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

/// <summary>
/// Event published when a Dapr sidecar becomes available.
/// </summary>
public class DaprSidecarAvailableEvent(IDaprSidecarResource resource, IServiceProvider services)
    : IDistributedApplicationResourceEvent
{
    /// <summary>
    /// Gets the Dapr sidecar resource that became available.
    /// </summary>
    public IResource Resource { get; } = resource;

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    public IServiceProvider Services { get; } = services;
}