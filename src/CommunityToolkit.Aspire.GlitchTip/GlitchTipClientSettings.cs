// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.GlitchTip;

/// <summary>Configures the GlitchTip client. The hosting integration supplies these values.</summary>
public sealed class GlitchTipClientSettings
{
    /// <summary>The configuration section used by the client.</summary>
    public const string SectionName = "Aspire:GlitchTip";

    /// <summary>Gets or sets the authoritative AppHost environment name.</summary>
    public string? Environment { get; set; }

    /// <summary>Gets or sets the release identifier shared with this service's deployment artifacts.</summary>
    public string? Release { get; set; }

    /// <summary>Gets or sets the stable Aspire resource name used as the service.name tag.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Gets or sets whether error events are reported. Defaults to true.</summary>
    public bool EnableErrors { get; set; } = true;

    /// <summary>Gets or sets whether structured SDK logs are reported. Defaults to false.</summary>
    public bool EnableLogs { get; set; }

    /// <summary>Gets or sets whether the existing OpenTelemetry tracing pipeline also reports to GlitchTip.</summary>
    public bool EnableTracing { get; set; }

    /// <summary>
    /// Gets or sets Sentry's transaction sample rate between zero and one. Defaults to one when tracing is enabled.
    /// OpenTelemetry sampling remains application-owned and also applies.
    /// </summary>
    public double TracesSampleRate { get; set; } = 1;

    /// <summary>Gets or sets the maximum shutdown flush duration, greater than zero and at most 30 seconds. Defaults to two seconds.</summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(2);
}
