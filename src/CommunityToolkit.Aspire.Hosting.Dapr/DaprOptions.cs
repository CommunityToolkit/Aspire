// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

/// <summary>
/// Options for configuring Dapr.
/// </summary>
[AspireExport(ExposeProperties = true)]
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
    /// <remarks>This property is not available in polyglot app hosts.</remarks>
    [AspireExportIgnore(Reason = "Action<IResource, DaprSidecarOptions?> callbacks are not ATS-compatible.")]
    public Action<IResource, DaprSidecarOptions?>? PublishingConfigurationAction { get; set; }
}
