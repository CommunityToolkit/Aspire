using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.MassTransit.RabbitMQ;
using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting;
    
/// <summary>
/// MassTransitHostingExtensions provides extension methods for configuring MassTransit in an Aspire-based application.
/// </summary>
public static class MassTransitHostingExtensions
{
    /// <summary>
    /// AddMassTransit configures a RabbitMQ-backed MassTransit integration in an Aspire-based application.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="name">A unique name for the RabbitMQ instance.</param>
    /// <param name="configure">An optional configuration action to override default settings.</param>
    public static IResourceBuilder<RabbitMQServerResource> AddMassTransit(this IDistributedApplicationBuilder builder, [ResourceName] string name, Action<MassTransitRabbitMqOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        // Load options from configuration
        IConfigurationSection configurationSection = builder.Configuration.GetSection($"MassTransit:{name}");
        MassTransitRabbitMqOptions rabbitMqOptions = new();
        configurationSection.Bind(rabbitMqOptions);

        // Apply additional configuration overrides
        configure?.Invoke(rabbitMqOptions);

        // Secure parameterized resources for username and password
        ParameterResource usernameResource = CreateParameterResource($"{name}Username", rabbitMqOptions.Username);
        ParameterResource passwordResource = CreateParameterResource($"{name}Password", rabbitMqOptions.Password, secret: true);

        // Register RabbitMQ
        return builder.AddRabbitMQ(
                name: name,
                port: rabbitMqOptions.Port ?? 5672,
                userName: builder.CreateResourceBuilder(usernameResource),
                password: builder.CreateResourceBuilder(passwordResource))
            .WithExternalHttpEndpoints()
            .WithManagementPlugin();
    }

    /// <summary>
    /// Creates a secure or non-secure parameter resource for Aspire's resource system.
    /// </summary>
    private static ParameterResource CreateParameterResource(string key, string value, bool secret = false)
    {
        Func<ParameterDefault?, string> callback = _ => value;
        var resource = new ParameterResource(key, callback, secret: secret);
        return resource;
    }
}


