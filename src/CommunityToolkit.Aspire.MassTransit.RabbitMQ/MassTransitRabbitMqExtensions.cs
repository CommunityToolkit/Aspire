using CommunityToolkit.Aspire.MassTransit.RabbitMQ;
using MassTransit;
using MassTransit.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
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
    /// <param name="configureOptions">Optional action to override default settings.</param>
    /// <param name="massTransitConfiguration">Action to register one or more consumers.</param>
    public static void AddMassTransitRabbitMq(
        this IHostApplicationBuilder builder,
        string name,
        Action<MassTransitRabbitMqOptions>? configureOptions = null,
        Action<IBusRegistrationConfigurator>? massTransitConfiguration = null)
    {
        AddMassTransitRabbitMq<IBus>(builder, name, configureOptions, massTransitConfiguration);
    }

    /// <summary>
    /// Configures an additional MassTransit bus instance with RabbitMQ integration.
    /// </summary>
    /// <typeparam name="TBus">The interface type representing the bus.</typeparam>
    /// <param name="builder">The client IHostApplicationBuilder.</param>
    /// <param name="name">A unique name for the RabbitMQ instance.</param>
    /// <param name="configureOptions">Optional action to override default settings.</param>
    /// <param name="massTransitConfiguration">Action to register one or more consumers.</param>
    public static void AddMassTransitRabbitMq<TBus>(
        this IHostApplicationBuilder builder,
        string name,
        Action<MassTransitRabbitMqOptions>? configureOptions = null,
        Action<IBusRegistrationConfigurator>? massTransitConfiguration = null)
        where TBus : class, IBus
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var options = new MassTransitRabbitMqOptions();
        configureOptions?.Invoke(options);
        var rabbitMqConnectionString = builder.Configuration["ConnectionStrings:" + name];

        if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
        {
            throw new InvalidOperationException(
                "RabbitMQ connection string is missing or empty in configuration.");
        }

        builder.Services.AddMassTransit<TBus>(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.SetInMemorySagaRepositoryProvider();

            var entryAssembly = Assembly.GetEntryAssembly();
            x.AddSagaStateMachines(entryAssembly);
            x.AddSagas(entryAssembly);
            x.AddActivities(entryAssembly);

            massTransitConfiguration?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitMqConnectionString));

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