using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides shared service configuration for the Tempo example.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    /// <summary>
    /// Adds telemetry, health checks, service discovery, and HTTP resilience.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The host application builder.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });
        return builder;
    }

    /// <summary>
    /// Configures telemetry for the Aspire dashboard and trace export to Tempo.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The host application builder.</returns>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var hasDashboardExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            if (hasDashboardExporter)
            {
                logging.AddOtlpExporter();
            }
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                if (hasDashboardExporter)
                {
                    metrics.AddOtlpExporter();
                }
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .SetSampler(new AlwaysOnSampler())
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath) &&
                            !context.Request.Path.StartsWithSegments(AlivenessEndpointPath);
                    })
                    .AddHttpClientInstrumentation();
                if (hasDashboardExporter)
                {
                    tracing.AddOtlpExporter();
                }
                if (builder.Configuration["TEMPO_OTLP_ENDPOINT"] is { Length: > 0 } tempoEndpoint)
                {
                    tracing.AddOtlpExporter("tempo", options =>
                    {
                        options.Endpoint = new Uri(tempoEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
            });

        return builder;
    }

    /// <summary>
    /// Adds the service's default liveness health check.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The host application builder.</returns>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    /// <summary>
    /// Maps readiness and liveness endpoints in the development environment.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application.</returns>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains("live")
            });
        }
        return app;
    }
}