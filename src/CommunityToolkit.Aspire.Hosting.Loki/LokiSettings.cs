using Aspire.Hosting.ApplicationModel;

#pragma warning disable ASPIREATS001

namespace Aspire.Hosting;

/// <summary>
/// Settings for configuring a Grafana Loki resource.
/// </summary>
[AspireExport(ExposeProperties = true)]
public class LokiSettings
{
    /// <summary>
    /// Gets or sets the tag to use for the Loki image.
    /// </summary>
    public string LokiTag { get; set; } = "3.6";

    /// <summary>
    /// Gets or sets the container registry for the image.
    /// </summary>
    public string Registry { get; set; } = "docker.io";

    /// <summary>
    /// Gets or sets the Loki image path.
    /// </summary>
    public string Image { get; set; } = "grafana/loki";

    /// <summary>
    /// Gets the full Loki image reference without the tag.
    /// </summary>
    public string LokiImage => $"{Registry}/{Image}";

    /// <summary>
    /// Gets or sets the host HTTP port. A random port is assigned when omitted.
    /// </summary>
    public int? HttpPort { get; set; }

    /// <summary>
    /// Gets or sets the host gRPC port. A random port is assigned when omitted.
    /// </summary>
    public int? GrpcPort { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the gRPC endpoint is enabled.
    /// </summary>
    public bool EnableGrpcEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Loki readiness health check is disabled.
    /// </summary>
    public bool DisableHealthcheck { get; set; }
}

#pragma warning restore ASPIREATS001