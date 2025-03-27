using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Extensions for adding OpenTelemetry Collector to the Aspire AppHost
/// </summary>
public static class CollectorExtensions
{
    private const string DashboardOtlpUrlVariableName = "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpApiKeyVariableName = "AppHost:OtlpApiKey";
    private const string DashboardOtlpUrlDefaultValue = "http://localhost:18889";

    /// <summary>
    /// Adds an OpenTelemetry Collector into the Aspire AppHost
    /// </summary>
    /// <param name="builder">The builder</param>
    /// <param name="name">The name of the collector</param>
    /// <param name="settings">The settings for the collector</param>
    /// <returns></returns>
    public static IResourceBuilder<CollectorResource> AddOpenTelemetryCollector(this IDistributedApplicationBuilder builder,
        string name,
        OpenTelemetryCollectorSettings settings)
    {
        var url = builder.Configuration[DashboardOtlpUrlVariableName] ?? DashboardOtlpUrlDefaultValue;
        var isHttpsEnabled = url.StartsWith("https", StringComparison.OrdinalIgnoreCase);

        var dashboardOtlpEndpoint = ReplaceLocalhostWithContainerHost(url, builder.Configuration);

        var resource = new CollectorResource(name);
        var resourceBuilder = builder.AddResource(resource)
            .WithImage(settings.CollectorImage, settings.CollectorVersion)
            .WithEndpoint(port: 4317, targetPort: 4317, name: CollectorResource.GRPCEndpointName, scheme: "http")
            .WithEndpoint(port: 4318, targetPort: 4318, name: CollectorResource.HTTPEndpointName, scheme: "http")
            .WithEnvironment("ASPIRE_ENDPOINT", dashboardOtlpEndpoint)
            .WithEnvironment("ASPIRE_API_KEY", builder.Configuration[DashboardOtlpApiKeyVariableName]);


        if (isHttpsEnabled && builder.ExecutionContext.IsRunMode && builder.Environment.IsDevelopment())
        {
            DevCertHostingExtensions.RunWithHttpsDevCertificate(resourceBuilder, "HTTPS_CERT_FILE", "HTTPS_CERT_KEY_FILE", (certFilePath, certKeyPath) =>
            {
                // Set TLS details using YAML path via the command line. This allows the values to be added to the existing config file.
                // Setting the values in the config file doesn't work because adding the "tls" section always enables TLS, even if there is no cert provided.
                resourceBuilder.WithArgs(
                    $@"--config=yaml:${settings.CertificateFileLocator}: ""dev-certs/dev-cert.pem""",
                    $@"--config=yaml:${settings.KeyFileLocator}: ""dev-certs/dev-cert.key""");
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
    /// Adds an Additional config file to the collector
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configPath"></param>
    /// <returns></returns>
    public static IResourceBuilder<CollectorResource> AddConfig(this IResourceBuilder<CollectorResource> builder, string configPath)
    {
        var configFileInfo = new FileInfo(configPath);
        return builder.WithBindMount(configPath, $"/config/{configFileInfo.Name}")
            .WithArgs($"--config=/config/{configFileInfo.Name}");
    }
}