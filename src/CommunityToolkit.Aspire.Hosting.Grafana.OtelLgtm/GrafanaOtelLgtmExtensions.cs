using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding the Grafana OTel-LGTM observability stack to a <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class GrafanaOtelLgtmExtensions
{
    private const string DefaultImage = "grafana/otel-lgtm";
    private const string DefaultTag = "0.21.0";
    private const int GrafanaPort = 3000;
    private const int OtlpGrpcPort = 4317;
    private const int OtlpHttpPort = 4318;

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
    /// <param name="grafanaPort">Optional host port for the Grafana web UI.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GrafanaOtelLgtmResource> AddGrafanaOtelLgtm(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? grafanaPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        GrafanaOtelLgtmResource resource = new(name);

        return builder.AddResource(resource)
            .WithImage(DefaultImage, DefaultTag)
            .WithEndpoint(targetPort: GrafanaPort, port: grafanaPort, name: GrafanaOtelLgtmResource.GrafanaEndpointName, scheme: "http")
            .WithEndpoint(targetPort: OtlpGrpcPort, name: GrafanaOtelLgtmResource.OtlpGrpcEndpointName, scheme: "http", isProxied: false)
            .WithEndpoint(targetPort: OtlpHttpPort, name: GrafanaOtelLgtmResource.OtlpHttpEndpointName, scheme: "http", isProxied: false)
            .WithHttpHealthCheck("/api/health", endpointName: GrafanaOtelLgtmResource.GrafanaEndpointName)
            .WithUrlForEndpoint(GrafanaOtelLgtmResource.GrafanaEndpointName, url => url.DisplayText = "Grafana")
            .WithIconName("ChartMultiple");
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
#pragma warning disable HAA0301, HAA0302, HAA0401
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
#pragma warning restore HAA0301, HAA0302, HAA0401

    /// <summary>
    /// Routes the telemetry for the resource through the specified Grafana OTel-LGTM instance.
    /// </summary>
    /// <typeparam name="T">The type of resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="lgtmBuilder">The Grafana OTel-LGTM resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
#pragma warning disable HAA0301, HAA0302, HAA0303
    public static IResourceBuilder<T> WithGrafanaOtelLgtmRouting<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<GrafanaOtelLgtmResource> lgtmBuilder) where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(lgtmBuilder, nameof(lgtmBuilder));

        builder.WithEnvironment(callback =>
        {
            var otlpProtocol = callback.EnvironmentVariables.GetValueOrDefault("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc");
            var endpointName = string.Equals(otlpProtocol.ToString(), "http/protobuf", StringComparison.Ordinal)
                ? GrafanaOtelLgtmResource.OtlpHttpEndpointName
                : GrafanaOtelLgtmResource.OtlpGrpcEndpointName;
            var endpoint = lgtmBuilder.Resource.GetEndpoint(endpointName);

            if (!callback.EnvironmentVariables.TryAdd("OTEL_EXPORTER_OTLP_ENDPOINT", endpoint))
            {
                callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint;
            }
        });
        builder.WithAnnotation(new WaitAnnotation(lgtmBuilder.Resource, WaitType.WaitUntilHealthy));

        return builder;
    }
#pragma warning restore HAA0301, HAA0302, HAA0303

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
    public static IResourceBuilder<GrafanaOtelLgtmResource> WithConfig(
        this IResourceBuilder<GrafanaOtelLgtmResource> builder,
        string configPath)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(configPath, nameof(configPath));

        return builder.WithBindMount(configPath, "/otel-lgtm/otelcol-config.yaml");
    }

    /// <summary>
    /// Sets an environment variable on the Grafana OTel-LGTM container.
    /// </summary>
    /// <remarks>
    /// Use this to configure the container, for example <c>ENABLE_LOGS_ALL=true</c> for logging
    /// or <c>GF_SECURITY_ADMIN_PASSWORD=secret</c> for custom Grafana credentials.
    /// </remarks>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The environment variable name.</param>
    /// <param name="value">The environment variable value.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GrafanaOtelLgtmResource> WithEnvironmentVariable(
        this IResourceBuilder<GrafanaOtelLgtmResource> builder,
        string name,
        string value)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        return builder.WithEnvironment(name, value);
    }
}
