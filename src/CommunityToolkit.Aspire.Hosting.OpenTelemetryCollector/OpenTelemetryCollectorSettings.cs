using Aspire.Hosting.ApplicationModel;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Settings for configuring an OpenTelemetry Collector resource.
/// </summary>
[AspireExport(ExposeProperties = true)]
public class OpenTelemetryCollectorSettings
{
    /// <summary>
    /// Gets or sets the tag to use for the collector image.
    /// </summary>
    public string CollectorTag { get; set; } = "latest";

    /// <summary>
    /// Gets or sets the container registry for the image.
    /// </summary>
    public string Registry { get; set; } = "ghcr.io";

    /// <summary>
    /// Gets or sets the collector image path.
    /// </summary>
    public string Image { get; set; } = "open-telemetry/opentelemetry-collector-releases/opentelemetry-collector-contrib";

    /// <summary>
    /// Gets the full collector image reference.
    /// </summary>
    public string CollectorImage { get => $"{Registry}/{Image}"; }

    /// <summary>
    /// Gets or sets a value indicating whether the default OTLP receivers should use HTTP even when Aspire is configured for HTTPS.
    /// </summary>
    public bool ForceNonSecureReceiver { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the gRPC endpoint is enabled on the collector container.
    /// 
    /// Note: this also configures TLS when Aspire is configured for HTTPS.
    /// </summary>
    public bool EnableGrpcEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the HTTP endpoint is enabled on the collector container.
    /// 
    /// Note: this also configures TLS when Aspire is configured for HTTPS.
    /// </summary>
    public bool EnableHttpEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the collector health check is disabled.
    /// </summary>
    public bool DisableHealthcheck { get; set; } = false;
}
