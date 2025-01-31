// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

/// <summary>
/// Indicates the options used to configure a Dapr sidecar.
/// </summary>
public sealed record DaprSidecarOptionsAnnotation(DaprSidecarOptions Options) : IResourceAnnotation
{
}
