using MassTransit;
using MassTransit.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// MassTransitClientExtensions provides extension methods for configuring MassTransit in a client application.
/// </summary>
public static class MassTransitClientExtensions
{
    /// <summary>
    /// Configures MassTransit with RabbitMQ integration for the client side,
    /// using the same configuration used by the hosting environment.
    /// </summary>
    /// <param name="builder">The client IhostApplicationBuilder</param>
    /// <param name="name">A unique name for the RabbitMQ instance.</param>
    /// <param name="telemetry">Enables telemetry, which could be exported to either OpenTelemetry or Application Insights.</param>
    public static void AddMassTransitRabbitMqClient(this IHostApplicationBuilder builder, string name, bool? telemetry = false)
    {
ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        var configuration = builder.Services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        var configurationSection = configuration.GetSection($"MassTransit:{name}");
        var options = new MassTransitOptions();
        configurationSection.Bind(options);

        builder.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.SetInMemorySagaRepositoryProvider();
            var entryAssembly = Assembly.GetEntryAssembly();
            x.AddConsumers(entryAssembly);
            x.AddSagaStateMachines(entryAssembly);
            x.AddSagas(entryAssembly);
            x.AddActivities(entryAssembly);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.Host, options.VirtualHost, h =>
                {
                    if (!string.IsNullOrEmpty(options.Username))
                        h.Username(options.Username);
                    if (!string.IsNullOrEmpty(options.Password))
                        h.Password(options.Password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        if (telemetry == true)
        {
            builder.Services.AddOpenTelemetry()
                .WithMetrics(b => b
                    .AddMeter(InstrumentationOptions.MeterName)
                ).WithTracing(builder =>
                {
                    builder
                        .AddSource(InstrumentationOptions.MeterName);
                });
        }
    }
}

/// <summary>
/// Configuration options for MassTransit client integration 
/// </summary>
internal sealed class MassTransitOptions
{
    public string Host { get; set; } = "localhost";
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}