using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for routing resource telemetry through an OpenTelemetry Collector.
/// </summary>
public static class OpenTelemetryCollectorRoutingExtensions
{
    /// <summary>
    /// Routes telemetry for the resource through the specified OpenTelemetry Collector.
    /// </summary>
    /// <typeparam name="T">The type of the resource being configured.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="collectorBuilder">The OpenTelemetry Collector resource builder.</param>
    /// <returns>A reference to the resource builder.</returns>
    [AspireExport("withOpenTelemetryCollectorRouting", Description = "Routes telemetry for a resource through the specified OpenTelemetry Collector")]
    public static IResourceBuilder<T> WithOpenTelemetryCollectorRouting<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<OpenTelemetryCollectorResource> collectorBuilder)
        where T : IResourceWithEnvironment
    {
        builder.WithEnvironment(callback =>
        {
            var otlpProtocol = callback.EnvironmentVariables.GetValueOrDefault("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc");
            var endpoint = collectorBuilder.Resource.GetEndpoint(otlpProtocol.ToString() ?? "grpc");
            callback.Logger.LogDebug("Forwarding Telemetry for {name} to the collector on {endpoint}", builder.Resource.Name, endpoint.Url);

            if (!callback.EnvironmentVariables.TryAdd("OTEL_EXPORTER_OTLP_ENDPOINT", endpoint))
            {
                callback.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint;
            }
        });
        builder.WithAnnotation(new WaitAnnotation(collectorBuilder.Resource, WaitType.WaitUntilHealthy));

        return builder;
    }
}

#pragma warning restore ASPIREATS001 // AspireExport is experimental
