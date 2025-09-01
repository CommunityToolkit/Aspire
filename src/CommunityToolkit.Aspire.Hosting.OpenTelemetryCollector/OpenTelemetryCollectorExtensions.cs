using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods to add the collector resource
/// </summary>
public static class OpenTelemetryCollectorExtensions
{
    private const string DashboardOtlpUrlVariableNameLegacy = "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpUrlVariableName = "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpApiKeyVariableName = "AppHost:OtlpApiKey";
    private const string DashboardOtlpUrlDefaultValue = "http://localhost:18889";

    /// <summary>
    /// Adds an OpenTelemetry Collector into the Aspire AppHost
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="configureSettings"></param>
    /// <returns></returns>
    public static IResourceBuilder<OpenTelemetryCollectorResource> AddOpenTelemetryCollector(this IDistributedApplicationBuilder builder,
        string name,
        Action<OpenTelemetryCollectorSettings>? configureSettings = null)
    {
        var url = builder.Configuration[DashboardOtlpUrlVariableName] ??
            builder.Configuration[DashboardOtlpUrlVariableNameLegacy] ??
            DashboardOtlpUrlDefaultValue;

        var settings = new OpenTelemetryCollectorSettings();
        configureSettings?.Invoke(settings);

        var isHttpsEnabled = !settings.ForceNonSecureReceiver && url.StartsWith("https", StringComparison.OrdinalIgnoreCase);

        var dashboardOtlpEndpoint = ReplaceLocalhostWithContainerHost(url, builder.Configuration);

        var resource = new OpenTelemetryCollectorResource(name);
        var resourceBuilder = builder.AddResource(resource)
            .WithImage(settings.CollectorImage, settings.CollectorTag)
            .WithEnvironment("ASPIRE_ENDPOINT", dashboardOtlpEndpoint)
            .WithEnvironment("ASPIRE_API_KEY", builder.Configuration[DashboardOtlpApiKeyVariableName]);

        if (settings.EnableGrpcEndpoint)
            resourceBuilder.WithEndpoint(targetPort: 4317, name: OpenTelemetryCollectorResource.GrpcEndpointName, scheme: isHttpsEnabled ? "https" : "http");
        if (settings.EnableHttpEndpoint)
            resourceBuilder.WithEndpoint(targetPort: 4318, name: OpenTelemetryCollectorResource.HttpEndpointName, scheme: isHttpsEnabled ? "https" : "http");


        if (!settings.ForceNonSecureReceiver && isHttpsEnabled && builder.ExecutionContext.IsRunMode && builder.Environment.IsDevelopment())
        {
            resourceBuilder.RunWithHttpsDevCertificate();
            var certFilePath = Path.Combine(DevCertHostingExtensions.DEV_CERT_BIND_MOUNT_DEST_DIR, DevCertHostingExtensions.CERT_FILE_NAME);
            var certKeyPath = Path.Combine(DevCertHostingExtensions.DEV_CERT_BIND_MOUNT_DEST_DIR, DevCertHostingExtensions.CERT_KEY_FILE_NAME);
            if (settings.EnableHttpEndpoint)
            {
                resourceBuilder.WithArgs(
                    $@"--config=yaml:receivers::otlp::protocols::http::tls::cert_file: ""{certFilePath}""",
                    $@"--config=yaml:receivers::otlp::protocols::http::tls::key_file: ""{certKeyPath}""");
            }
            if (settings.EnableGrpcEndpoint)
            {
                resourceBuilder.WithArgs(
                    $@"--config=yaml:receivers::otlp::protocols::grpc::tls::cert_file: ""{certFilePath}""",
                    $@"--config=yaml:receivers::otlp::protocols::grpc::tls::key_file: ""{certKeyPath}""");
            }
        }
        return resourceBuilder;
    }

    /// <summary>
    /// Force all apps to forward to the collector instead of the dashboard directly
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IResourceBuilder<OpenTelemetryCollectorResource> WithAppForwarding(this IResourceBuilder<OpenTelemetryCollectorResource> builder)
    {
        builder.AddEnvironmentVariablesEventHook()
               .WithFirstStartup();

        return builder;
    }

    private static string ReplaceLocalhostWithContainerHost(string value, IConfiguration configuration)
    {
        var hostName = configuration["AppHost:ContainerHostname"] ?? "host.docker.internal";

        return value.Replace("localhost", hostName, StringComparison.OrdinalIgnoreCase)
                    .Replace("127.0.0.1", hostName)
                    .Replace("[::1]", hostName);
    }

    /// <summary>
    /// Adds a config file to the collector
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configPath"></param>
    /// <returns></returns>
    public static IResourceBuilder<OpenTelemetryCollectorResource> WithConfig(this IResourceBuilder<OpenTelemetryCollectorResource> builder, string configPath)
    {
        var configFileInfo = new FileInfo(configPath);
        return builder.WithBindMount(configPath, $"/config/{configFileInfo.Name}")
            .WithArgs($"--config=/config/{configFileInfo.Name}");
    }

    /// <summary>
    /// Sets up the OnBeforeResourceStarted event to add a wait annotation to all resources that have the OtlpExporterAnnotation
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    private static IResourceBuilder<OpenTelemetryCollectorResource> WithFirstStartup(this IResourceBuilder<OpenTelemetryCollectorResource> builder)
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
    private static IResourceBuilder<OpenTelemetryCollectorResource> AddEnvironmentVariablesEventHook(this IResourceBuilder<OpenTelemetryCollectorResource> builder)
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