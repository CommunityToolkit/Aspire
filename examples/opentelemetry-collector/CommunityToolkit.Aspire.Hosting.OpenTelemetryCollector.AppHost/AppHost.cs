var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_OpenTelemetryCollector_Api>("api");

var collector = builder.AddOpenTelemetryCollector("opentelemetry-collector")
    .WithAppForwarding()
    .WithConfig("./config.yaml");

AddTelemetryGenerator("telemetrygen-traces", "traces", "GanttChart");
AddTelemetryGenerator("telemetrygen-metrics", "metrics", "ChartMultiple");
AddTelemetryGenerator("telemetrygen-logs", "logs", "SlideTextSparkle");
builder.Build().Run();

IResourceBuilder<ContainerResource> AddTelemetryGenerator(string name, string signal, string iconName)
{
    return builder.AddContainer(name, "open-telemetry/opentelemetry-collector-contrib/telemetrygen")
        .WithImageTag("latest")
        .WithImageRegistry("ghcr.io")
        .WithIconName(iconName)
        .WithOtlpExporter()
        .WithArgs(signal,
            "--duration", "inf",
            "--otlp-endpoint", collector.GetEndpoint("grpc").Property(EndpointProperty.HostAndPort))
        .WithArgs(args)
        .WithParentRelationship(collector);
}