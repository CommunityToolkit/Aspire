// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Checks a successful deployment before its initializer publishes Running.
/// The latest apply outcome is available through <see cref="KindDeploymentOutcomeAnnotation"/>.
/// </summary>
internal interface IKindPostApplyCheck
{
    Task CheckAsync(KindDeployedResource resource, CancellationToken cancellationToken);
}
