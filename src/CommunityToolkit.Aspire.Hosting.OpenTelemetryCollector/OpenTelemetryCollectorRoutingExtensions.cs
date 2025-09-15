using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Hooks to add the OTLP environment variables to the various resources
/// </summary>
public static class OpenTelemetryCollectorRoutingExtensions
{
    /// <summary>
    /// Resource the telemetry for the resource through the specified OpenTelemetry Collector
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="collectorBuilder"></param>
    /// <returns></returns>
    public static IResourceBuilder<T> WithOpenTelemetryCollectorRouting<T>(this IResourceBuilder<T> builder, IResourceBuilder<OpenTelemetryCollectorResource> collectorBuilder) where T : IResourceWithEnvironment
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
