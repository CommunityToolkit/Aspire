using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding the Grafana OTel-LGTM observability stack to a <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class GrafanaOtelLgtmExtensions
{
    private const string DashboardOtlpUrlVariableNameLegacy = "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpUrlVariableName = "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpUrlDefaultValue = "http://localhost:18889";
    private const int GrafanaPort = 3000;
    private const int OtlpGrpcPort = 4317;
    private const int OtlpHttpPort = 4318;
    private const int PrometheusPort = 9090;
    private const int PyroscopePort = 4040;

    /// <summary>
    /// Adds the Grafana OTel-LGTM observability stack to the application builder.
    /// </summary>
    /// <remarks>
    /// The <c>grafana/otel-lgtm</c> Docker image bundles the OpenTelemetry Collector,
    /// Prometheus, Loki, Tempo, Pyroscope, and Grafana into a single container.
    /// It accepts OTLP signals on ports 4317 (gRPC) and 4318 (HTTP) and exposes
    /// the Grafana web UI on port 3000.
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configureSettings">Optional action to configure <see cref="GrafanaOtelLgtmSettings"/>.</param>
    /// <param name="grafanaPort">Optional host port for the Grafana web UI.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GrafanaOtelLgtmResource> AddGrafanaOtelLgtm(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        Action<GrafanaOtelLgtmSettings>? configureSettings = null,
        int? grafanaPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        var settings = new GrafanaOtelLgtmSettings();
        configureSettings?.Invoke(settings);

        var url = builder.Configuration[DashboardOtlpUrlVariableName] ??
            builder.Configuration[DashboardOtlpUrlVariableNameLegacy] ??
            DashboardOtlpUrlDefaultValue;

        var useHttpsForReceivers = !settings.ForceNonSecureReceiver && url.StartsWith("https", StringComparison.OrdinalIgnoreCase);

        GrafanaOtelLgtmResource resource = new(name);

        var resourceBuilder = builder.AddResource(resource)
            .WithImage(settings.Image, settings.Tag)
            .WithEndpoint(targetPort: GrafanaPort, port: grafanaPort, name: GrafanaOtelLgtmResource.GrafanaEndpointName, scheme: "http")
            .WithEndpoint(targetPort: PrometheusPort, name: GrafanaOtelLgtmResource.PrometheusEndpointName, scheme: "http")
            .WithEndpoint(targetPort: PyroscopePort, name: GrafanaOtelLgtmResource.PyroscopeEndpointName, scheme: "http")
            .WithHttpHealthCheck("/api/health", endpointName: GrafanaOtelLgtmResource.GrafanaEndpointName)
            .WithUrlForEndpoint(GrafanaOtelLgtmResource.GrafanaEndpointName, url => url.DisplayText = "Grafana")
            .WithUrlForEndpoint(GrafanaOtelLgtmResource.PrometheusEndpointName, url => url.DisplayText = "Prometheus")
            .WithUrlForEndpoint(GrafanaOtelLgtmResource.PyroscopeEndpointName, url => url.DisplayText = "Pyroscope")
            .WithIconName("ChartMultiple");

        if (settings.EnableGrpcEndpoint)
        {
            ConfigureReceiver(resourceBuilder, OtlpGrpcPort, GrafanaOtelLgtmResource.OtlpGrpcEndpointName, useHttpsForReceivers);
        }

        if (settings.EnableHttpEndpoint)
        {
            ConfigureReceiver(resourceBuilder, OtlpHttpPort, GrafanaOtelLgtmResource.OtlpHttpEndpointName, useHttpsForReceivers);
        }

        return resourceBuilder;
    }

    private static void ConfigureReceiver(IResourceBuilder<GrafanaOtelLgtmResource> resourceBuilder, int port, string endpointName, bool useHttpsForReceivers)
    {
        var scheme = useHttpsForReceivers ? "https" : "http";
        resourceBuilder
            .WithEndpoint(targetPort: port, name: endpointName, scheme: scheme)
            .WithUrlForEndpoint(endpointName, url => url.DisplayLocation = UrlDisplayLocation.DetailsOnly);

        if (!useHttpsForReceivers)
        {
            return;
        }

#pragma warning disable ASPIRECERTIFICATES001
        //resourceBuilder.WithHttpsCertificateConfiguration(ctx =>
        //{
        //    ctx.Arguments.Add(ReferenceExpression.Create($@"--config=yaml:receivers::otlp::protocols::{endpointName}::tls::cert_file: ""{ctx.CertificatePath}"""));
        //    ctx.Arguments.Add(ReferenceExpression.Create($@"--config=yaml:receivers::otlp::protocols::{endpointName}::tls::key_file: ""{ctx.KeyPath}"""));
        //    return Task.CompletedTask;
        //});
#pragma warning restore ASPIRECERTIFICATES001
    }

    /// <summary>
    /// Adds a named volume for persisting Grafana OTel-LGTM data across container restarts.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to a name based on the resource name.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GrafanaOtelLgtmResource> WithDataVolume(
        this IResourceBuilder<GrafanaOtelLgtmResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.WithVolume(name ?? $"{builder.Resource.Name}-data", "/data");
    }

    /// <summary>
    /// Configures all resources with OpenTelemetry exporters to route their telemetry through this Grafana OTel-LGTM instance.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GrafanaOtelLgtmResource> WithAppForwarding(
        this IResourceBuilder<GrafanaOtelLgtmResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
        {
            var otelSenders = evt.Model.Resources
                .OfType<IResourceWithEnvironment>()
                .Where(x => x.HasAnnotationOfType<OtlpExporterAnnotation>());

            foreach (var otelSender in otelSenders)
            {
                var otelSenderBuilder = builder.ApplicationBuilder.CreateResourceBuilder(otelSender);
                otelSenderBuilder.WithGrafanaOtelLgtmRouting(builder);
            }

            return Task.CompletedTask;
        });

        return builder;
    }

    /// <summary>
    /// Routes the telemetry for the resource through the specified Grafana OTel-LGTM instance.
    /// </summary>
    /// <typeparam name="T">The type of resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="lgtmBuilder">The Grafana OTel-LGTM resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithGrafanaOtelLgtmRouting<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<GrafanaOtelLgtmResource> lgtmBuilder) where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(lgtmBuilder, nameof(lgtmBuilder));

        builder.WithEnvironment(callback =>
        {
            var otlpProtocol = callback.EnvironmentVariables.GetValueOrDefault("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc");
            var otlpProtocolValue = otlpProtocol?.ToString();

            string endpointName;
            if (!string.IsNullOrWhiteSpace(otlpProtocolValue) &&
                otlpProtocolValue.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                endpointName = GrafanaOtelLgtmResource.OtlpHttpEndpointName;
            }
            else
            {
                endpointName = GrafanaOtelLgtmResource.OtlpGrpcEndpointName;
            }

            string endpoint;
            try
            {
                endpoint = lgtmBuilder.Resource.GetEndpoint(endpointName);
            }
            catch (System.Exception ex) when (ex is System.InvalidOperationException || ex is System.ArgumentException)
            {
                string fallbackEndpointName = endpointName == GrafanaOtelLgtmResource.OtlpHttpEndpointName
                    ? GrafanaOtelLgtmResource.OtlpGrpcEndpointName
                    : GrafanaOtelLgtmResource.OtlpHttpEndpointName;

                try
                {
                    endpoint = lgtmBuilder.Resource.GetEndpoint(fallbackEndpointName);
                }
                catch (System.Exception)
                {
                    throw new System.InvalidOperationException(
                        $"The requested OTLP protocol '{otlpProtocolValue}' maps to endpoint '{endpointName}', but no corresponding endpoint is configured on the Grafana OTel-LGTM resource, and no fallback endpoint '{fallbackEndpointName}' is available.",
                        ex);
                }
            }

            if (!callback.EnvironmentVariables.TryAdd("OTEL_EXPORTER_OTLP_ENDPOINT", endpoint))
            {
                callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint;
            }
        });
        builder.WithAnnotation(new WaitAnnotation(lgtmBuilder.Resource, WaitType.WaitUntilHealthy));

        return builder;
    }

    /// <summary>
    /// Adds a config file to the OpenTelemetry Collector inside the Grafana OTel-LGTM container.
    /// </summary>
    /// <remarks>
    /// The configuration file is mounted at <c>/otel-lgtm/otelcol-config.yaml</c>, which replaces
    /// the default collector configuration inside the container.
    /// </remarks>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configPath">The path to the OpenTelemetry Collector configuration YAML file.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GrafanaOtelLgtmResource> WithCollectorConfig(
        this IResourceBuilder<GrafanaOtelLgtmResource> builder,
        string configPath)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(configPath, nameof(configPath));

        return builder.WithBindMount(configPath, "/otel-lgtm/otelcol-config.yaml");
    }

    /// <summary>
    /// Mounts a custom Grafana configuration file into the Grafana OTel-LGTM container.
    /// </summary>
    /// <remarks>
    /// Grafana is also configurable via <c>GF_*</c> environment variables.
    /// See the <a href="https://grafana.com/docs/grafana/latest/setup-grafana/configure-grafana/">Grafana documentation</a> for details.
    /// </remarks>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configPath">The path to the Grafana configuration file (<c>grafana.ini</c> or <c>custom.ini</c>).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GrafanaOtelLgtmResource> WithGrafanaConfig(
        this IResourceBuilder<GrafanaOtelLgtmResource> builder,
        string configPath)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(configPath, nameof(configPath));

        return builder.WithBindMount(configPath, "/otel-lgtm/grafana/conf/custom.ini", isReadOnly: true);
    }

    /// <summary>
    /// Mounts a custom Prometheus configuration file into the Grafana OTel-LGTM container.
    /// </summary>
    /// <remarks>
    /// Prometheus also supports additional CLI flags via the <c>PROMETHEUS_EXTRA_ARGS</c> environment variable.
    /// </remarks>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configPath">The path to the Prometheus configuration YAML file.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GrafanaOtelLgtmResource> WithPrometheusConfig(
        this IResourceBuilder<GrafanaOtelLgtmResource> builder,
        string configPath)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(configPath, nameof(configPath));

        return builder.WithBindMount(configPath, "/otel-lgtm/prometheus.yaml", isReadOnly: true);
    }

}
