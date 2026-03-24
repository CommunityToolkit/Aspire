// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Annotation that tracks whether a container has been connected to the Kind Docker network.
/// </summary>
internal sealed class KindNetworkConnectedAnnotation : IResourceAnnotation;
