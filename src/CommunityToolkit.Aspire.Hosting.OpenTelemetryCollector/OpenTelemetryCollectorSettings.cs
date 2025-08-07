namespace Aspire.Hosting;

/// <summary>
/// Settings for the OpenTelemetry Collector
/// </summary>
public class OpenTelemetryCollectorSettings
{
    /// <summary>
    /// The version of the collector, defaults to latest
    /// </summary>
    public string CollectorVersion { get; set; } = "latest";

    /// <summary>
    /// The image of the collector, defaults to ghcr.io/open-telemetry/opentelemetry-collector-releases/opentelemetry-collector-contrib
    /// </summary>
    public string CollectorImage { get; set; } = "ghcr.io/open-telemetry/opentelemetry-collector-releases/opentelemetry-collector-contrib";

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
