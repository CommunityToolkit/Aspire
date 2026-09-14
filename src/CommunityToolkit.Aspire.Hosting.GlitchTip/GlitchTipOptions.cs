// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip;

/// <summary>Independent reporting controls, forwarded to referencing applications.</summary>
[AspireDto]
public sealed class GlitchTipTelemetryOptions
{
    /// <summary>Gets or sets whether errors are reported. Defaults to true.</summary>
    public bool EnableErrors { get; set; } = true;
    /// <summary>Gets or sets whether SDK logs are reported. Defaults to false.</summary>
    public bool EnableLogs { get; set; }
    /// <summary>Gets or sets whether tracing is enabled. Defaults to false.</summary>
    public bool EnableTracing { get; set; }
    /// <summary>Gets or sets the trace sampling fraction, between zero and one.</summary>
    public double TracesSampleRate { get; set; } = 1;
}

/// <summary>Polling settings for HTTP health-check monitors.</summary>
[AspireDto]
public sealed class GlitchTipMonitorOptions
{
    /// <summary>Gets or sets the polling interval in seconds. Defaults to 60.</summary>
    public int IntervalSeconds { get; set; } = 60;
    /// <summary>Gets or sets the timeout in seconds. Defaults to 20.</summary>
    public int TimeoutSeconds { get; set; } = 20;
}

internal sealed record GlitchTipConsumerAnnotation(GlitchTipResource Project) : IResourceAnnotation;
internal sealed record GlitchTipReleaseAnnotation(ParameterResource Release) : IResourceAnnotation;
internal sealed record GlitchTipTelemetryAnnotation(GlitchTipTelemetryOptions Options) : IResourceAnnotation;
internal sealed record GlitchTipMonitorAnnotation(string Identity, EndpointReference Endpoint, string Path, int StatusCode) : IResourceAnnotation
{
    public bool Enabled { get; init; } = true;
    public ReferenceExpression? Url { get; init; }
    public GlitchTipMonitorOptions? Options { get; init; }
}