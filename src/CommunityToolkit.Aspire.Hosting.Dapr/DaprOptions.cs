// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

/// <summary>
/// Options for configuring Dapr.
/// </summary>
public sealed record DaprOptions
{
    /// <summary>
    /// Gets or sets the path to the Dapr CLI.
    /// </summary>
    public string? DaprPath { get; set; }

    /// <summary>
    /// Gets or sets whether Dapr sidecars export telemetry to the Aspire dashboard.
    /// </summary>
    /// <remarks>
    /// Telemetry is enabled by default.
    /// </remarks>
    public bool? EnableTelemetry { get; set; }

    /// <summary>
    /// Gets or sets the action to be executed during the publishing process.
    /// </summary>
    /// <remarks>This property allows customization of the publishing behavior by assigning a delegate that
    /// defines the desired operation.</remarks>
    public Action<IResource, DaprSidecarOptions?>? PublishingConfigurationAction { get; set; }
}
