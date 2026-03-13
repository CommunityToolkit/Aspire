namespace Aspire.Hosting;

/// <summary>
/// Settings for the Grafana OTel-LGTM observability stack.
/// </summary>
public class GrafanaOtelLgtmSettings
{
    /// <summary>
    /// The tag to use for the container image.
    /// </summary>
    public string Tag { get; set; } = "0.21.0";

    /// <summary>
    /// The container image name.
    /// </summary>
    public string Image { get; set; } = "grafana/otel-lgtm";

    /// <summary>
    /// Force the default OTLP receivers in the collector to use HTTP even if Aspire is set to HTTPS.
    /// </summary>
    public bool ForceNonSecureReceiver { get; set; }

    /// <summary>
    /// Enable the gRPC endpoint on the embedded OpenTelemetry Collector (port 4317).
    /// </summary>
    /// <remarks>
    /// This will also set up SSL if Aspire is configured for HTTPS.
    /// </remarks>
    public bool EnableGrpcEndpoint { get; set; } = true;

    /// <summary>
    /// Enable the HTTP endpoint on the embedded OpenTelemetry Collector (port 4318).
    /// </summary>
    /// <remarks>
    /// This will also set up SSL if Aspire is configured for HTTPS.
    /// </remarks>
    public bool EnableHttpEndpoint { get; set; } = true;
}
