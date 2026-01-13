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

        var resource = new OpenTelemetryCollectorResource(name);
        var resourceBuilder = builder.AddResource(resource)
            .WithImage(settings.CollectorImage, settings.CollectorTag)
            .WithEnvironment("ASPIRE_ENDPOINT", new HostUrl(url))
            .WithEnvironment("ASPIRE_API_KEY", builder.Configuration[DashboardOtlpApiKeyVariableName])
            .WithIconName("DesktopPulse");

        var useHttpsForReceivers = !settings.ForceNonSecureReceiver && url.StartsWith("https", StringComparison.OrdinalIgnoreCase);

        if (settings.EnableGrpcEndpoint)
            ConfigureReceiver(4317, OpenTelemetryCollectorResource.GrpcEndpointName);

        if (settings.EnableHttpEndpoint)
            ConfigureReceiver(4318, OpenTelemetryCollectorResource.HttpEndpointName);

        if (!settings.DisableHealthcheck)
        {
            const int healthPort = 13233;
            resourceBuilder.WithEndpoint(targetPort: healthPort, name: "health", scheme: "http")
                .WithHttpHealthCheck("/health", endpointName: "health")
                .WithArgs(
                    "--feature-gates=confmap.enableMergeAppendOption",
                    $"--config=yaml:extensions::health_check/aspire::endpoint: 0.0.0.0:{healthPort}",
                    "--config=yaml:service::extensions: [ health_check/aspire ]"
                    );
        }
        return resourceBuilder;

        void ConfigureReceiver(int port, string protocol)
        {
            var scheme = useHttpsForReceivers ? "https" : "http";
            resourceBuilder.WithEndpoint(targetPort: port, name: protocol, scheme: scheme);

            if (!useHttpsForReceivers)
            {
                return;
            }

#pragma warning disable ASPIRECERTIFICATES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            resourceBuilder.WithHttpsCertificateConfiguration(ctx =>
            {
                ctx.Arguments.Add(ReferenceExpression.Create($@"--config=yaml:receivers::otlp::protocols::{protocol}::tls::cert_file: ""{ctx.CertificatePath}"""));
                ctx.Arguments.Add(ReferenceExpression.Create($@"--config=yaml:receivers::otlp::protocols::{protocol}::tls::key_file: ""{ctx.KeyPath}"""));
                return Task.CompletedTask;
            });
#pragma warning restore ASPIRECERTIFICATES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        }
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