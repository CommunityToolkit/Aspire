// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Dapr component resource.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="DaprComponentResource"/>.
/// </remarks>
/// <param name="name">The resource name.</param>
/// <param name="type">The Dapr component type. This may be a generic "state" or "pubsub" if Aspire should choose an appropriate type when running or deploying.</param>
public sealed class DaprComponentResource(string name, string type) : Resource(name), IResourceWithWaitSupport
{
    /// <inheritdoc/>
    public string Type { get; } = type;

    /// <inheritdoc/>
    public DaprComponentOptions? Options { get; init; }
}
