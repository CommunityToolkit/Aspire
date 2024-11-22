using CommunityToolkit.Aspire.Client.MassTransit.RabbitMQ;
using MassTransit;
using MassTransit.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// MassTransitClientExtensions provides extension methods for configuring MassTransit in a client application.
/// </summary>
public static class MassTransitRabbitMqExtensions
{
    /// <summary>
    /// Configures MassTransit with RabbitMQ integration for the client side,
    /// using the same configuration used by the hosting environment.
    /// </summary>
    /// <param name="builder">The client IHostApplicationBuilder.</param>
    /// <param name="name">A unique name for the RabbitMQ instance.</param>
    /// <param name="configure">Optional action to override default settings.</param>
    public static void AddMassTransitRabbitMq(
        this IHostApplicationBuilder builder,
        string name,
        Action<MassTransitRabbitMqOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        // Load options from configuration
        var serviceProvider = builder.Services.BuildServiceProvider();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var configurationSection = configuration.GetSection($"Aspire:MassTransit:RabbitMQ");

        var options = new MassTransitRabbitMqOptions();
        configurationSection.Bind(options);

        // Apply additional configuration overrides
        configure?.Invoke(options);

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
                // Configure RabbitMQ host
                cfg.Host(options.Host, options.VirtualHost, h =>
                {
                    // Resolve username and password values from IResourceBuilder<ParameterResource>
                    h.Username(options.UsernameKey?.Resource?.Value ?? "guest");
                    h.Password(options.PasswordKey?.Resource?.Value ?? "guest");
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        // Telemetry configuration
        if (!options.DisableTelemetry)
        {
            builder.Services.AddOpenTelemetry()
                .WithMetrics(b => b.AddMeter(InstrumentationOptions.MeterName))
                .WithTracing(providerBuilder =>
                {
                    providerBuilder.AddSource(InstrumentationOptions.MeterName);
                });
        }
    }
}