// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

/// <summary>
/// Indicates that a Dapr sidecar should be started for the associated resource.
/// </summary>
public sealed record DaprSidecarAnnotation(IDaprSidecarResource Sidecar) : IResourceAnnotation
{
}
