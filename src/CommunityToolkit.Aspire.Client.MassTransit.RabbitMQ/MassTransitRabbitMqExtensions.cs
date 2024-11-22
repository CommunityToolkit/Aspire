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
    /// <param name="builder">The client IHostApplicationBuilder</param>
    /// <param name="name">A unique name for the RabbitMQ instance.</param>
    /// <param name="configure">Overwrites for configurations such as telemetry</param>
    public static void AddMassTransitRabbitMq(
            this IHostApplicationBuilder builder,
            string name,
            Action<MassTransitRabbitMqOptions>? configure = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

            // Load options from configuration
            var configuration = builder.Services.BuildServiceProvider().GetRequiredService<IConfiguration>();
            var configurationSection = configuration.GetSection($"MassTransit:{name}");
            
            //todo add the rest of the configuration (host,vhost,usern,passw..)
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
                    var host = configurationSection.GetValue<string>("Host", "localhost");
                    var virtualHost = configurationSection.GetValue<string>("VirtualHost", "/");
                    var username = configurationSection.GetValue<string>("Username", string.Empty);
                    var password = configurationSection.GetValue<string>("Password", string.Empty);

                    cfg.Host(host, virtualHost, h =>
                    {
                        if (!string.IsNullOrEmpty(username))
                            h.Username(username);
                        if (!string.IsNullOrEmpty(password))
                            h.Password(password);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            });

            if (!options.DisableTelemetry)
            {
                builder.Services.AddOpenTelemetry()
                    .WithMetrics(b => b
                        .AddMeter(InstrumentationOptions.MeterName)
                    ).WithTracing(providerBuilder =>
                    {
                        providerBuilder
                            .AddSource(InstrumentationOptions.MeterName);
                    });
            }
        }
}
