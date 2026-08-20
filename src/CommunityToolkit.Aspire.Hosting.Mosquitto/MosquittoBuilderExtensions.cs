using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Mosquitto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Mosquitto resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class MosquittoBuilderExtensions
{
    private const string DataTarget = "/mosquitto/data";

    // The default mosquitto.conf shipped with the resource. Without a `listener` directive Mosquitto
    // runs in local-only mode and refuses connections from outside the container (including the DCP
    // port proxy and therefore the health check), so this configuration exposes the MQTT endpoint on
    // all interfaces and enables anonymous access, which matches the default Mosquitto behavior when
    // no password file is configured. Persistence is enabled and pointed at the data folder so that
    // the WithDataVolume/WithDataBindMount APIs actually persist state.
    private const string DefaultConfig = """
        listener 1883
        allow_anonymous true
        persistence true
        persistence_location /mosquitto/data/
        """;

    /// <summary>
    /// Adds a Mosquitto container resource to the application. Mosquitto is an open source MQTT
    /// message broker, so the resource can be referenced by any MQTT client integration.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="MosquittoContainerImageTags.Tag"/> tag of the <inheritdoc cref="MosquittoContainerImageTags.Image"/> container image.
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource. This name is used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port that the MQTT endpoint is exposed on. If <see langword="null"/> a random port is assigned.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<MosquittoServerResource> AddMosquitto(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        MosquittoServerResource resource = new(name);

        // The health check captures the connection string in its factory closure, so cache the check
        // (and its underlying MQTT client) once the connection string is available instead of creating
        // a new client on each health poll. Before the connection string is published the factory
        // throws, which keeps the resource unhealthy until it is ready without caching a broken instance.
        string? connectionString = null;
        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(resource, async (@event, ct) =>
        {
            connectionString = await resource.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false) 
            ?? 
            throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{resource.Name}' resource but the connection string was null.");
        });

        var healthCheckKey = $"{name}_check";

        MqttHealthCheck? mqttHealthCheck = null;

        var healthCheckRegistration = new HealthCheckRegistration(
            healthCheckKey,
            _ => mqttHealthCheck ??= new MqttHealthCheck(connectionString ?? throw new InvalidOperationException("Connection string is unavailable")),
            failureStatus: default,
            tags: default
        );

        builder.Services.AddHealthChecks().Add(healthCheckRegistration);

        return builder.AddResource(resource)
            .WithImage(MosquittoContainerImageTags.Image, MosquittoContainerImageTags.Tag)
            .WithImageRegistry(MosquittoContainerImageTags.Registry)
            .WithEndpoint(
                port: port,
                targetPort: MosquittoServerResource.DefaultPort,
                name: MosquittoServerResource.PrimaryEndpointName,
                scheme: MosquittoServerResource.PrimaryEndpointScheme)
            .WithContainerFiles("/mosquitto/config", [new ContainerFile { Name = "mosquitto.conf", Contents = DefaultConfig }])
            .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a Mosquitto container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<MosquittoServerResource> WithDataVolume(this IResourceBuilder<MosquittoServerResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), DataTarget, isReadOnly);
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a Mosquitto container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<MosquittoServerResource> WithDataBindMount(this IResourceBuilder<MosquittoServerResource> builder, string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return builder.WithBindMount(source, DataTarget, isReadOnly);
    }
}
