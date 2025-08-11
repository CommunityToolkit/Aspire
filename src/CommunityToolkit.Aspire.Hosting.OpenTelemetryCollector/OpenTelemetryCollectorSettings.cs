namespace Aspire.Hosting;

/// <summary>
/// Settings for the OpenTelemetry Collector
/// </summary>
public class OpenTelemetryCollectorSettings
{
    /// <summary>
    /// The Tag to use for the collector
    /// </summary>
    public string CollectorTag { get; set; } = "latest";

    /// <summary>
    /// The registry for the image
    /// </summary>
    public string Registry { get; set; } = "ghcr.io";

    /// <summary>
    /// The collector image path
    /// </summary>
    public string Image { get; set; } = "open-telemetry/opentelemetry-collector-releases/opentelemetry-collector-contrib";

    /// <summary>
    /// The image of the collector, defaults to ghcr.io/open-telemetry/opentelemetry-collector-releases/opentelemetry-collector-contrib
    /// </summary>
    public string CollectorImage { get => $"{Registry}/{Image}"; }

    /// <summary>
    /// Force the default OTLP receivers in the collector to use HTTP even if Aspire is set to HTTPS
    /// </summary>
    public bool ForceNonSecureReceiver { get; set; } = false;

    /// <summary>
    /// Enable the gRPC endpoint on the collector container (requires the relevant collector config)
    /// 
    /// Note: this will also setup SSL if Aspire is configured for HTTPS
    /// </summary>
    public bool EnableGrpcEndpoint { get; set; } = true;

    /// <summary>
    /// Enable the HTTP endpoint on the collector container (requires the relevant collector config)
    /// 
    /// Note: this will also setup SSL if Aspire is configured for HTTPS
    /// </summary>
    public bool EnableHttpEndpoint { get; set; } = true;
}
