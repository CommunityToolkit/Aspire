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
    /// The name of the collector, defaults to the default otlp receiver
    /// </summary>
    public string CertificateFileLocator { get; set; } = "receivers::otlp::protocols::grpc::tls::cert_file";

    /// <summary>
    /// The name of the collector, defaults to the default otlp receiver
    /// </summary>
    public string KeyFileLocator { get; set; } = "receivers::otlp::protocols::grpc::tls::key_file";
}
