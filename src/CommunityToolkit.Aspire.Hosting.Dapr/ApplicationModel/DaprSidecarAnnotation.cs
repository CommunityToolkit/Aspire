// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Indicates that a Dapr sidecar should be started for the associated resource.
/// </summary>
public sealed record DaprSidecarAnnotation(DaprSidecarResource Sidecar) : IResourceAnnotation;
