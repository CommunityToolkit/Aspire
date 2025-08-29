using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Hooks to add the OTLP environment variables to the various resources
/// </summary>
public static class EnvironmentVariableEventExtention
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IResourceBuilder<OpenTelemetryCollectorResource> WithFirstStartup(this IResourceBuilder<OpenTelemetryCollectorResource> builder)
    {
        builder.OnBeforeResourceStarted((resource, beforeStartedEvent, cancellationToken) =>
        {
            var logger = beforeStartedEvent.Services.GetRequiredService<ResourceLoggerService>().GetLogger(resource);
            var appModel = beforeStartedEvent.Services.GetRequiredService<DistributedApplicationModel>();
            var resources = appModel.GetProjectResources();
            var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().FirstOrDefault();

            if (collectorResource is null)
            {
                logger.LogWarning("No collector resource found");
                return Task.CompletedTask;
            }
            foreach (var resourceItem in resources.Where(r => r.HasAnnotationOfType<OtlpExporterAnnotation>()))
            {
                resourceItem.Annotations.Add(new WaitAnnotation(collectorResource, WaitType.WaitUntilHealthy));
            }
            return Task.CompletedTask;
        });
        return builder;
    }

    /// <summary>
    /// Sets up the OnResourceEndpointsAllocated event to add/update the OTLP environment variables for the collector to the various resources
    /// </summary>
    /// <param name="builder"></param>
    public static IResourceBuilder<OpenTelemetryCollectorResource> AddEnvironmentVariablesEventHook(this IResourceBuilder<OpenTelemetryCollectorResource> builder)
    {
        builder.OnResourceEndpointsAllocated((resource, allocatedEvent, cancellationToken) =>
        {
            var logger = allocatedEvent.Services.GetRequiredService<ResourceLoggerService>().GetLogger(resource);
            var appModel = allocatedEvent.Services.GetRequiredService<DistributedApplicationModel>();
            var resources = appModel.GetProjectResources();
            var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().FirstOrDefault();

            if (collectorResource is null)
            {
                logger.LogWarning("No collector resource found");
                return Task.CompletedTask;
            }

            var grpcEndpoint = collectorResource.GetEndpoint(collectorResource!.GrpcEndpoint.EndpointName);
            var httpEndpoint = collectorResource.GetEndpoint(collectorResource!.HttpEndpoint.EndpointName);

            if (!resources.Any())
            {
                logger.LogInformation("No resources to add Environment Variables to");
            }

            foreach (var resourceItem in resources.Where(r => r.HasAnnotationOfType<OtlpExporterAnnotation>()))
            {
                logger.LogDebug("Forwarding Telemetry for {name} to the collector", resourceItem.Name);
                if (resourceItem is null) continue;

                resourceItem.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
                {
                    var protocol = context.EnvironmentVariables.GetValueOrDefault("OTEL_EXPORTER_OTLP_PROTOCOL", "");
                    var endpoint = protocol.ToString() == "http/protobuf" ? httpEndpoint : grpcEndpoint;

                    if (endpoint is null)
                    {
                        logger.LogWarning("No {protocol} endpoint on the collector for {resourceName} to use",
                            protocol, resourceItem.Name);
                        return;
                    }

                    if (context.EnvironmentVariables.ContainsKey("OTEL_EXPORTER_OTLP_ENDPOINT"))
                        context.EnvironmentVariables.Remove("OTEL_EXPORTER_OTLP_ENDPOINT");
                    context.EnvironmentVariables.Add("OTEL_EXPORTER_OTLP_ENDPOINT", endpoint.Url);
                }));
            }

            return Task.CompletedTask;
        });

        return builder;
    }
}
