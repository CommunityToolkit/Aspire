using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Hooks to add the OTLP environment variables to the various containers
/// </summary>
/// <param name="logger"></param>
public class EnvironmentVariableHook(ILogger<EnvironmentVariableHook> logger) : IDistributedApplicationLifecycleHook
{
    /// <inheritdoc />
    public Task AfterEndpointsAllocatedAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
    {
        var resources = appModel.GetProjectResources();
        var collectorResource = appModel.Resources.OfType<CollectorResource>().FirstOrDefault();

        if (collectorResource is null)
        {
            logger.LogWarning("No collector resource found");
            return Task.CompletedTask;
        }

        var grpcEndpoint = collectorResource!.GetEndpoint(collectorResource!.GRPCEndpoint.EndpointName);
        var grpcEndpoint = collectorResource.GetEndpoint(collectorResource.GRPCEndpoint.EndpointName);
        var httpEndpoint = collectorResource.GetEndpoint(collectorResource.HTTPEndpoint.EndpointName);

        if (!resources.Any())
        {
            logger.LogInformation("No resources to add Environment Variables to");
        }

        foreach (var resourceItem in resources)
        {
            logger.LogDebug("Forwarding Telemetry for {name} to the collector", resourceItem.Name);
            if (resourceItem is null) continue;

            resourceItem.Annotations.Add(new EnvironmentCallbackAnnotation((EnvironmentCallbackContext context) =>
            {
                var protocol = context.EnvironmentVariables.GetValueOrDefault("OTEL_EXPORTER_OTLP_PROTOCOL", "");
                var endpoint = protocol.ToString() == "http/protobuf" ? httpEndpoint : grpcEndpoint;

                if (endpoint == null)
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
    }
}
