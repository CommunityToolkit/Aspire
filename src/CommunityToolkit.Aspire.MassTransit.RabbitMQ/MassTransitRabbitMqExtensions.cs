using CommunityToolkit.Aspire.MassTransit.RabbitMQ;
using MassTransit;
using MassTransit.Logging;
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
    /// <param name="configureConsumers">Action to register one or more consumers.</param>
    public static void AddMassTransitRabbitMq(
        this IHostApplicationBuilder builder,
        string name,
        Action<MassTransitRabbitMqOptions>? configure = null,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var options = new MassTransitRabbitMqOptions();
        configure?.Invoke(options);

        builder.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.SetInMemorySagaRepositoryProvider();

            var entryAssembly = Assembly.GetEntryAssembly();
            x.AddSagaStateMachines(entryAssembly);
            x.AddSagas(entryAssembly);
            x.AddActivities(entryAssembly);

            // Register consumers using the provided action
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqConnectionString = builder.Configuration["ConnectionStrings:" + name];
                if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
                {
                    throw new InvalidOperationException("RabbitMQ connection string is missing or empty in configuration.");
                }
                cfg.Host(new Uri(rabbitMqConnectionString));

                // Configure endpoints as the last step
                cfg.ConfigureEndpoints(context);
            });
        });

        if (!options.DisableTelemetry)
        {
            builder.Services.AddOpenTelemetry()
                .WithMetrics(b => b.AddMeter(DiagnosticHeaders.DefaultListenerName))
                .WithTracing(providerBuilder =>
                {
                    providerBuilder.AddSource(DiagnosticHeaders.DefaultListenerName);
                });
        }
    }
}