// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.GlitchTip;

namespace Aspire.Hosting;

/// <summary>Preserves HTTP health-check declarations for GlitchTip uptime monitoring.</summary>
public static class GlitchTipMonitoringExtensions
{
    /// <summary>Adds an Aspire HTTP health check and retains its public monitoring metadata.</summary>
    /// <typeparam name="T">The project or container resource.</typeparam>
    /// <param name="builder">The resource.</param>
    /// <param name="path">The health-check path.</param>
    /// <param name="endpointName">The HTTP endpoint name.</param>
    /// <param name="statusCode">The expected HTTP status.</param>
    /// <returns>The original resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithGlitchTipHealthCheck<T>(this IResourceBuilder<T> builder,
        string path = "/health", string endpointName = "http", int statusCode = 200) where T : IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal)) throw new ArgumentException("A health-check path must begin with a single slash.", nameof(path));
        if (statusCode < 100 || statusCode > 599) throw new ArgumentOutOfRangeException(nameof(statusCode));
        var identity = $"{endpointName}:{path}";
        if (builder.Resource.Annotations.OfType<GlitchTipMonitorAnnotation>().Any(a => a.Identity == identity)) throw new ArgumentException("The HTTP health check is already registered.", nameof(path));
        builder.WithHttpHealthCheck(path, statusCode, endpointName);
        return builder.WithAnnotation(new GlitchTipMonitorAnnotation(identity, builder.GetEndpoint(endpointName), path, statusCode));
    }

    /// <summary>Sets stack-wide polling defaults.</summary>
    /// <param name="builder">The GlitchTip project.</param>
    /// <param name="options">The default timing settings.</param>
    /// <returns>The original builder.</returns>
    [AspireExport]
    public static IResourceBuilder<GlitchTipResource> WithMonitorDefaults(this IResourceBuilder<GlitchTipResource> builder, GlitchTipMonitorOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Validate(options);
        builder.Resource.MonitorDefaults = options;
        return builder;
    }

    /// <summary>Configures one declared HTTP check's monitoring without changing its Aspire health check.</summary>
    /// <typeparam name="T">The monitored resource.</typeparam>
    /// <param name="builder">The consumer.</param>
    /// <param name="path">The declared health-check path.</param>
    /// <param name="endpointName">The declared endpoint name.</param>
    /// <param name="enabled">Whether to manage a monitor for this check.</param>
    /// <param name="options">Optional timing overrides.</param>
    /// <returns>The original builder.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithGlitchTipMonitor<T>(this IResourceBuilder<T> builder, string path = "/health", string endpointName = "http", bool enabled = true, GlitchTipMonitorOptions? options = null) where T : IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (options is not null) Validate(options);
        var annotation = Find(builder.Resource, endpointName, path);
        builder.Resource.Annotations.Remove(annotation);
        return builder.WithAnnotation(annotation with { Enabled = enabled, Options = options });
    }

    /// <summary>Overrides a check's monitor-facing URL with a deferred reference expression.</summary>
    /// <typeparam name="T">The monitored resource.</typeparam>
    /// <param name="builder">The consumer.</param>
    /// <param name="url">The URL as seen by the GlitchTip server.</param>
    /// <param name="path">The declared health-check path.</param>
    /// <param name="endpointName">The declared endpoint name.</param>
    /// <returns>The original builder.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithGlitchTipMonitorUrl<T>(this IResourceBuilder<T> builder, ReferenceExpression url, string path = "/health", string endpointName = "http") where T : IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(url);
        var annotation = Find(builder.Resource, endpointName, path);
        builder.Resource.Annotations.Remove(annotation);
        return builder.WithAnnotation(annotation with { Url = url });
    }

    private static GlitchTipMonitorAnnotation Find(IResource resource, string endpointName, string path)
    {
        var identity = $"{endpointName}:{path}";
        var existing = resource.Annotations.OfType<GlitchTipMonitorAnnotation>().SingleOrDefault(a => a.Identity == identity);
        if (existing is not null) return existing;
#pragma warning disable ASPIREPROBES001
        var probe = resource.Annotations.OfType<EndpointProbeAnnotation>()
            .FirstOrDefault(a => a.EndpointReference.EndpointName == endpointName && a.Path == path);
#pragma warning restore ASPIREPROBES001
        return probe is null
            ? throw new ArgumentException("Declare the HTTP check with WithGlitchTipHealthCheck or an Aspire endpoint probe before configuring its monitor.")
            : new GlitchTipMonitorAnnotation(identity, probe.EndpointReference, path, 200);
    }

    private static void Validate(GlitchTipMonitorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.IntervalSeconds is < 1 or > 86400 || options.TimeoutSeconds is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(options), "GlitchTip requires an interval between 1 and 86400 seconds and a timeout between 1 and 60 seconds.");
    }
}