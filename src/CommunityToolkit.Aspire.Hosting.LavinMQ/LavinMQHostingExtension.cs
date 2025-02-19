using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.LavinMQ;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring and adding a LavinMQ container as a resource
/// within a distributed application using Aspire.Hosting. This enables connection, health
/// checks, and specific configurations for LavinMQ instances.
/// </summary>
public static class LavinMQHostingExtension
{
    /// <summary>
    /// Adds a LavinMQ container resource to the distributed application builder.
    /// Configures the resource with specified parameters and sets up health checks for the resource.
    /// </summary>
    /// <param name="builder">The distributed application builder to which the LavinMQ resource will be added.</param>
    /// <param name="name">The name of the LavinMQ resource.</param>
    /// <param name="amqpPort">The port number for the AMQP protocol. Default is 5672.</param>
    /// <param name="managementPort">The port number for the management interface. Default is 15672.</param>
    /// <returns>A resource builder for the LavinMQ container resource.</returns>
    /// <exception cref="DistributedApplicationException">Thrown when the resource addition fails or other errors occur during the process.</exception>
    public static IResourceBuilder<LavinMQContainerResource> AddLavinMQ(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int amqpPort = LavinMQContainerResource.DefaultAmqpPort,
        int managementPort = LavinMQContainerResource.DefaultManagementPort)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        LavinMQContainerResource instance = new LavinMQContainerResource(name);

        string? connectionString = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(instance, async (_, ct) =>
        {
            connectionString = await instance.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);

            if (connectionString is null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{instance.Name}' resource but the connection string was null.");
            }
        });

        string healthCheckKey = $"{name}_check";
        // cache the connection so it is reused on subsequent calls to the health check
        IConnection? connection = null;
        builder.Services.AddHealthChecks().AddRabbitMQ(async _ =>
        {
            // NOTE: Ensure that execution of this setup callback is deferred until after
            //       the container is built & started.
            return connection ??= await CreateConnection(connectionString!).ConfigureAwait(false);

            static Task<IConnection> CreateConnection(string connectionString)
            {
                ConnectionFactory factory = new ConnectionFactory
                {
                    Uri = new(connectionString),
                };
                return factory.CreateConnectionAsync();
            }
        }, healthCheckKey);

        return builder.AddResource(instance)
                      .WithImage(LavinMQContainerImageSettings.Image, LavinMQContainerImageSettings.Tag)
                      .WithImageRegistry(LavinMQContainerImageSettings.Registry)
                      .WithEndpoint(
                          port: amqpPort,
                          targetPort: LavinMQContainerResource.DefaultAmqpPort,
                          name: LavinMQContainerResource.PrimaryEndpointName,
                          scheme: LavinMQContainerResource.PrimaryEndpointSchema)
                      .WithEndpoint(
                          port: managementPort,
                          targetPort: LavinMQContainerResource.DefaultManagementPort,
                          name: LavinMQContainerResource.ManagementEndpointName,
                          scheme: LavinMQContainerResource.ManagementEndpointSchema)
                      .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Configures a data volume for the LavinMQ container resource by specifying its name and read-only status.
    /// </summary>
    /// <param name="builder">The resource builder for the LavinMQ container resource.</param>
    /// <param name="name">The name of the data volume to be attached to the LavinMQ container resource.</param>
    /// <param name="isReadOnly">Indicates whether the data volume should be mounted as read-only. Default is false.</param>
    /// <returns>The updated resource builder for the LavinMQ container resource.</returns>
    public static IResourceBuilder<LavinMQContainerResource> WithDataVolume(this IResourceBuilder<LavinMQContainerResource> builder, string name,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name, "/var/lib/lavinmq", isReadOnly);
    }

    /// <summary>
    /// Configures a bind mount for the LavinMQ container resource to allow data persistence.
    /// The method mounts a specified source path on the host to the container's data directory.
    /// </summary>
    /// <param name="builder">The resource builder for the LavinMQ container to which the data bind mount will be added.</param>
    /// <param name="source">The source path on the host machine to bind mount to the container's data directory.</param>
    /// <param name="isReadOnly">Indicates if the bind mount should be configured as read-only. Default is false.</param>
    /// <returns>An updated resource builder for the LavinMQ container resource with the configured data bind mount.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the builder or source parameters are null.</exception>
    public static IResourceBuilder<LavinMQContainerResource> WithDataBindMount(this IResourceBuilder<LavinMQContainerResource> builder, string source,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/var/lib/lavinmq", isReadOnly);
    }
}