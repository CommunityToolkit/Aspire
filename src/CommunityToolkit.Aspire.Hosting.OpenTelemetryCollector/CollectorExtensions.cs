using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods to add the collector resource
/// </summary>
public static class CollectorExtensions
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
    public static IResourceBuilder<CollectorResource> AddOpenTelemetryCollector(this IDistributedApplicationBuilder builder,
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

        var resource = new CollectorResource(name);
        var resourceBuilder = builder.AddResource(resource)
            .WithImage(settings.CollectorImage, settings.CollectorVersion)
            .WithEnvironment("ASPIRE_ENDPOINT", dashboardOtlpEndpoint)
            .WithEnvironment("ASPIRE_API_KEY", builder.Configuration[DashboardOtlpApiKeyVariableName]);

        if (settings.EnableGrpcEndpoint)
            resourceBuilder.WithEndpoint(targetPort: 4317, name: CollectorResource.GRPCEndpointName, scheme: isHttpsEnabled ? "https" : "http");
        if (settings.EnableHttpEndpoint)
            resourceBuilder.WithEndpoint(targetPort: 4318, name: CollectorResource.HTTPEndpointName, scheme: isHttpsEnabled ? "https" : "http");


        if (!settings.ForceNonSecureReceiver && isHttpsEnabled && builder.ExecutionContext.IsRunMode && builder.Environment.IsDevelopment())
        {
            DevCertHostingExtensions.RunWithHttpsDevCertificate(resourceBuilder, "HTTPS_CERT_FILE", "HTTPS_CERT_KEY_FILE", (certFilePath, certKeyPath) =>
            {
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
            });
        }
        return resourceBuilder;
    }

    /// <summary>
    /// Force all apps to forward to the collector instead of the dashboard directly
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IResourceBuilder<CollectorResource> WithAppForwarding(this IResourceBuilder<CollectorResource> builder)
    {
        builder.ApplicationBuilder.Services.TryAddLifecycleHook<EnvironmentVariableHook>();
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
    public static IResourceBuilder<CollectorResource> WithConfig(this IResourceBuilder<CollectorResource> builder, string configPath)
    {
        var configFileInfo = new FileInfo(configPath);
        return builder.WithBindMount(configPath, $"/config/{configFileInfo.Name}")
            .WithArgs($"--config=/config/{configFileInfo.Name}");
    }
}