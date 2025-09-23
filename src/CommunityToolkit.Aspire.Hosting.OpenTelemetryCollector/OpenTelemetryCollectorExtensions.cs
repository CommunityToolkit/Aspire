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

        var resource = new OpenTelemetryCollectorResource(name);
        var resourceBuilder = builder.AddResource(resource)
            .WithImage(settings.CollectorImage, settings.CollectorTag)
            .WithEnvironment("ASPIRE_ENDPOINT", new HostUrl(url))
            .WithEnvironment("ASPIRE_API_KEY", builder.Configuration[DashboardOtlpApiKeyVariableName]);

        if (settings.EnableGrpcEndpoint)
            resourceBuilder.WithEndpoint(targetPort: 4317, name: OpenTelemetryCollectorResource.GrpcEndpointName, scheme: isHttpsEnabled ? "https" : "http");
        if (settings.EnableHttpEndpoint)
            resourceBuilder.WithEndpoint(targetPort: 4318, name: OpenTelemetryCollectorResource.HttpEndpointName, scheme: isHttpsEnabled ? "https" : "http");


        if (!settings.ForceNonSecureReceiver && isHttpsEnabled && builder.ExecutionContext.IsRunMode && builder.Environment.IsDevelopment())
        {
            resourceBuilder.RunWithHttpsDevCertificate();

            // Not using `Path.Combine` as we MUST use unix style paths in the container
            var certFilePath = $"{DevCertHostingExtensions.DEV_CERT_BIND_MOUNT_DEST_DIR}/{DevCertHostingExtensions.CERT_FILE_NAME}";
            var certKeyPath = $"{DevCertHostingExtensions.DEV_CERT_BIND_MOUNT_DEST_DIR}/{DevCertHostingExtensions.CERT_KEY_FILE_NAME}";

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
        builder.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
        {
            var logger = evt.Services.GetRequiredService<ResourceLoggerService>().GetLogger(builder.Resource);
            var otelSenders = evt.Model.Resources
                .OfType<IResourceWithEnvironment>()
                .Where(x => x.HasAnnotationOfType<OtlpExporterAnnotation>());

            foreach (var otelSender in otelSenders)
            {
                var otelSenderBuilder = builder.ApplicationBuilder.CreateResourceBuilder(otelSender);
                otelSenderBuilder.WithOpenTelemetryCollectorRouting(builder);
            }

            return Task.CompletedTask;
        });

        return builder;
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

}