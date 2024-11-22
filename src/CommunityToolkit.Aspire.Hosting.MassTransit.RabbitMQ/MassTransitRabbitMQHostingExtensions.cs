using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.MassTransit.RabbitMQ;
using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting;
    
/// <summary>
/// MassTransitHostingExtensions provides extension methods for configuring MassTransit in an Aspire-based application.
/// </summary>
public static class MassTransitRabbitMqHostingExtensions
{
    
    /// <summary>
    /// AddMassTransit configures a RabbitMQ-backed MassTransit integration in an Aspire-based application.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="name">A unique name for the RabbitMQ instance.</param>
    /// <param name="configure">An optional configuration action to override default settings.</param>
    public static IResourceBuilder<RabbitMQServerResource> AddMassTransitRabbitMq(
        this IDistributedApplicationBuilder builder, [ResourceName] string name, Action<MassTransitRabbitMqOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        MassTransitRabbitMqOptions rabbitMqOptions = new();

        // Load options from configuration
        IConfigurationSection configurationSection = builder.Configuration.GetSection($"Aspire:MassTransit:RabbitMQ");
        configurationSection.Bind(rabbitMqOptions);

        // Create default parameters if not provided
        rabbitMqOptions.UsernameKey ??= builder.CreateResourceBuilder(ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"masstransit-rabbitmq-username"));
        rabbitMqOptions.PasswordKey ??= builder.CreateResourceBuilder(ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"masstransit-rabbitmq-password"));

        configure?.Invoke(rabbitMqOptions);

        return builder.AddRabbitMQ(
                name: name,
                port: rabbitMqOptions.Port ?? 5672,
                userName: rabbitMqOptions.UsernameKey,
                password: rabbitMqOptions.PasswordKey)
            .WithExternalHttpEndpoints()
            .WithManagementPlugin();
    }

 

}


