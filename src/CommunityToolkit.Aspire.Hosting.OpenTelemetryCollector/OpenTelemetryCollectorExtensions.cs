using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding and configuring OpenTelemetry Collector resources.
/// </summary>
public static class OpenTelemetryCollectorExtensions
{
    private const string DashboardOtlpUrlVariableNameLegacy = "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpUrlVariableName = "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpApiKeyVariableName = "AppHost:OtlpApiKey";
    private const string DashboardOtlpUrlDefaultValue = "http://localhost:18889";

    /// <summary>
    /// Adds an OpenTelemetry Collector container resource to the application model.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configureSettings">An optional callback that configures the collector settings.</param>
    /// <returns>A reference to the resource builder.</returns>
    [AspireExport("addOpenTelemetryCollector", Description = "Adds an OpenTelemetry Collector container resource")]
    public static IResourceBuilder<OpenTelemetryCollectorResource> AddOpenTelemetryCollector(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        Action<OpenTelemetryCollectorSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        string url = builder.Configuration[DashboardOtlpUrlVariableName] ??
            builder.Configuration[DashboardOtlpUrlVariableNameLegacy] ??
            DashboardOtlpUrlDefaultValue;

        OpenTelemetryCollectorSettings settings = new();
        configureSettings?.Invoke(settings);

        OpenTelemetryCollectorResource resource = new(name);
        IResourceBuilder<OpenTelemetryCollectorResource> resourceBuilder = builder.AddResource(resource)
            .WithImage(settings.Image, settings.CollectorTag)
            .WithImageRegistry(settings.Registry)
            .WithEnvironment("ASPIRE_ENDPOINT", new HostUrl(url))
            .WithEnvironment("ASPIRE_API_KEY", builder.Configuration[DashboardOtlpApiKeyVariableName])
            .WithIconName("DesktopPulse");

        bool useHttpsForReceivers = !settings.ForceNonSecureReceiver && url.StartsWith("https", StringComparison.OrdinalIgnoreCase);

        if (settings.EnableGrpcEndpoint)
        {
            ConfigureReceiver(4317, OpenTelemetryCollectorResource.GrpcEndpointName);
        }

        if (settings.EnableHttpEndpoint)
        {
            ConfigureReceiver(4318, OpenTelemetryCollectorResource.HttpEndpointName);
        }

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
            string scheme = useHttpsForReceivers ? "https" : "http";
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
    /// Configures all compatible resources in the application to forward telemetry to this collector.
    /// </summary>
    /// <param name="builder">The collector resource builder.</param>
    /// <returns>A reference to the resource builder.</returns>
    [AspireExport("withAppForwarding", Description = "Configures all compatible resources to forward telemetry to this collector")]
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
    /// Adds a configuration file to the collector resource.
    /// </summary>
    /// <param name="builder">The collector resource builder.</param>
    /// <param name="configPath">The path to the collector configuration file.</param>
    /// <returns>A reference to the resource builder.</returns>
    [AspireExport("withConfig", Description = "Adds a configuration file to the collector resource")]
    public static IResourceBuilder<OpenTelemetryCollectorResource> WithConfig(this IResourceBuilder<OpenTelemetryCollectorResource> builder, string configPath)
    {
        FileInfo configFileInfo = new(configPath);
        return builder.WithBindMount(configPath, $"/config/{configFileInfo.Name}")
            .WithArgs($"--config=/config/{configFileInfo.Name}");
    }
}

#pragma warning restore ASPIREATS001 // AspireExport is experimental
