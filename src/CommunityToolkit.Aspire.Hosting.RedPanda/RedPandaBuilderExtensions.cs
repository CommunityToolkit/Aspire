using System.Globalization;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.RedPanda;
using Confluent.Kafka;
using HealthChecks.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Redpanda resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class RedPandaBuilderExtensions
{
    private const string DataTarget = "/var/lib/redpanda/data";

    /// <summary>
    /// Adds a Redpanda container resource to the application. Redpanda is a Kafka API compatible
    /// streaming platform, so the resource can be referenced by any Kafka client integration.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="RedPandaContainerImageTags.Tag"/> tag of the <inheritdoc cref="RedPandaContainerImageTags.Image"/> container image.
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource. This name is used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port that the Kafka API is exposed on. If <see langword="null"/> a random port is assigned.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<RedPandaServerResource> AddRedPanda(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return builder.AddRedPandaCore(name, new RedPandaServerOptions(), port);
    }

    /// <summary>
    /// Adds a Redpanda container resource to the application, using a delegate to configure broker options
    /// such as the CPU and memory limits. Redpanda is a Kafka API compatible streaming platform, so the
    /// resource can be referenced by any Kafka client integration.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="RedPandaContainerImageTags.Tag"/> tag of the <inheritdoc cref="RedPandaContainerImageTags.Image"/> container image.
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource. This name is used as the connection string name when referenced in a dependency.</param>
    /// <param name="configureOptions">A delegate that configures the <see cref="RedPandaServerOptions"/> for the broker.</param>
    /// <param name="port">The host port that the Kafka API is exposed on. If <see langword="null"/> a random port is assigned.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExportIgnore(Reason = "Action<RedPandaServerOptions> is not ATS-compatible. Use the AddRedPanda(builder, name, port) overload instead.")]
    public static IResourceBuilder<RedPandaServerResource> AddRedPanda(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        Action<RedPandaServerOptions> configureOptions,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configureOptions);

        RedPandaServerOptions options = new();
        configureOptions(options);

        return builder.AddRedPandaCore(name, options, port);
    }

    private static IResourceBuilder<RedPandaServerResource> AddRedPandaCore(
        this IDistributedApplicationBuilder builder,
        string name,
        RedPandaServerOptions options,
        int? port)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.CpuCount, $"{nameof(options)}.{nameof(options.CpuCount)}");
        ArgumentException.ThrowIfNullOrEmpty(options.Memory, $"{nameof(options)}.{nameof(options.Memory)}");

        RedPandaServerResource resource = new(name);

        // The Admin API readiness probe (/v1/status/ready) reports when the Redpanda node has started,
        // but it can flip to ready a moment before the external Kafka listener is actually accepting
        // client connections. That gap is why consumers gated with WaitFor could still log a few
        // transient "Connection refused" errors at startup. Add a Kafka-level health check - a producer
        // that connects to the advertised bootstrap address, exactly as a real client would - so the
        // resource only reports healthy once the broker truly accepts Kafka connections. This mirrors
        // the health check that the built-in Aspire Kafka hosting integration registers.
        string? connectionString = null;
        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(resource, async (@event, ct) =>
        {
            connectionString = await resource.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);

            if (connectionString is null)
            {
                throw new DistributedApplicationException(
                    $"ConnectionStringAvailableEvent was published for the '{resource.Name}' resource but the connection string was null.");
            }
        });

        var kafkaHealthCheckKey = $"{name}_kafka_check";

        // Register directly rather than via AddKafkaHealthCheck: the health check captures the
        // connection string in its factory closure, so a per-resource registration avoids multiple
        // Redpanda resources sharing (and overwriting) a single health check instance.
        //
        // The registration factory runs on every health poll, so cache the check (and its underlying
        // Kafka producer) once it can be built - i.e. once the connection string is available - instead
        // of creating a new producer, and its background threads, on each interval. Before the
        // connection string is published the factory throws, which keeps the resource unhealthy until
        // it is ready without caching a broken instance.
        KafkaHealthCheck? kafkaHealthCheck = null;
        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            kafkaHealthCheckKey,
            _ => kafkaHealthCheck ??= new KafkaHealthCheck(new KafkaHealthCheckOptions
            {
                Configuration = new ProducerConfig
                {
                    BootstrapServers = connectionString ?? throw new InvalidOperationException("Connection string is unavailable")
                }
            }),
            failureStatus: default,
            tags: default));

        return builder.AddResource(resource)
            .WithImage(RedPandaContainerImageTags.Image, RedPandaContainerImageTags.Tag)
            .WithImageRegistry(RedPandaContainerImageTags.Registry)
            // The external Kafka listener advertises its own host address (localhost:{port}) so that
            // clients running on the host can reconnect after the initial metadata exchange. That only
            // works if the advertised port is the real host port, so the endpoint is not proxied and
            // the host port is published directly to the container's Kafka broker port.
            .WithEndpoint(targetPort: RedPandaServerResource.KafkaBrokerPort, port: port, name: RedPandaServerResource.PrimaryEndpointName, isProxied: false)
            .WithEndpoint(targetPort: RedPandaServerResource.KafkaInternalBrokerPort, name: RedPandaServerResource.InternalEndpointName)
            .WithHttpEndpoint(targetPort: RedPandaServerResource.SchemaRegistryPort, name: RedPandaServerResource.SchemaRegistryEndpointName)
            .WithHttpEndpoint(targetPort: RedPandaServerResource.AdminPort, name: RedPandaServerResource.AdminEndpointName)
            .WithEntrypoint("/usr/bin/rpk")
            .WithArgs(context => ConfigureRedPandaArgs(context, resource, options))
            .WithHttpHealthCheck("/v1/status/ready", endpointName: RedPandaServerResource.AdminEndpointName)
            .WithHealthCheck(kafkaHealthCheckKey);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a Redpanda container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<RedPandaServerResource> WithDataVolume(
        this IResourceBuilder<RedPandaServerResource> builder,
        string? name = null,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), DataTarget, isReadOnly);
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a Redpanda container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<RedPandaServerResource> WithDataBindMount(
        this IResourceBuilder<RedPandaServerResource> builder,
        string source,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return builder.WithBindMount(source, DataTarget, isReadOnly);
    }

    /// <summary>
    /// Adds a Redpanda Console container to the application, configured to connect to the Redpanda broker.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="RedPandaContainerImageTags.ConsoleTag"/> tag of the <inheritdoc cref="RedPandaContainerImageTags.ConsoleImage"/> container image.
    /// </remarks>
    /// <param name="builder">The Redpanda server resource builder.</param>
    /// <param name="configureContainer">An optional callback to configure the Redpanda Console container resource.</param>
    /// <param name="containerName">The name of the container (optional).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for the Redpanda server resource.</returns>
    [AspireExport(RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<RedPandaServerResource> WithConsole(
        this IResourceBuilder<RedPandaServerResource> builder,
        Action<IResourceBuilder<RedPandaConsoleContainerResource>>? configureContainer = null,
        string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        containerName ??= $"{builder.Resource.Name}-console";

        RedPandaConsoleContainerResource console = new(containerName);

        IResourceBuilder<RedPandaConsoleContainerResource> consoleBuilder = builder.ApplicationBuilder.AddResource(console)
            .WithImage(RedPandaContainerImageTags.ConsoleImage, RedPandaContainerImageTags.ConsoleTag)
            .WithImageRegistry(RedPandaContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: RedPandaConsoleContainerResource.HttpPort, name: RedPandaConsoleContainerResource.HttpEndpointName)
            .WithEnvironment(context => ConfigureConsoleContainer(context, builder.Resource))
            .WaitFor(builder)
            // Nest the Console under the Redpanda resource in the dashboard so the management UIs
            // appear as children of the broker they belong to.
            .WithParentRelationship(builder)
            .ExcludeFromManifest();

        configureContainer?.Invoke(consoleBuilder);

        return builder;
    }

    /// <summary>
    /// Configures the host port that the Redpanda Console resource is exposed on instead of using a randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder for the Redpanda Console.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> a random port is assigned.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<RedPandaConsoleContainerResource> WithHostPort(
        this IResourceBuilder<RedPandaConsoleContainerResource> builder,
        int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(RedPandaConsoleContainerResource.HttpEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Adds a Kafka UI container to the application, configured to connect to the Redpanda broker. This is the
    /// same Kafka management UI (the <c>kafbat/kafka-ui</c> image) used by the official Aspire Kafka integration,
    /// so it works against any Kafka API compatible broker.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="RedPandaContainerImageTags.KafkaUiTag"/> tag of the <inheritdoc cref="RedPandaContainerImageTags.KafkaUiImage"/> container image.
    /// </remarks>
    /// <param name="builder">The Redpanda server resource builder.</param>
    /// <param name="configureContainer">An optional callback to configure the Kafka UI container resource.</param>
    /// <param name="containerName">The name of the container (optional).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for the Redpanda server resource.</returns>
    [AspireExport(RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<RedPandaServerResource> WithKafkaUI(
        this IResourceBuilder<RedPandaServerResource> builder,
        Action<IResourceBuilder<RedPandaKafkaUiContainerResource>>? configureContainer = null,
        string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        containerName ??= $"{builder.Resource.Name}-kafka-ui";

        RedPandaKafkaUiContainerResource kafkaUi = new(containerName);

        IResourceBuilder<RedPandaKafkaUiContainerResource> kafkaUiBuilder = builder.ApplicationBuilder.AddResource(kafkaUi)
            .WithImage(RedPandaContainerImageTags.KafkaUiImage, RedPandaContainerImageTags.KafkaUiTag)
            .WithImageRegistry(RedPandaContainerImageTags.KafkaUiRegistry)
            .WithHttpEndpoint(targetPort: RedPandaKafkaUiContainerResource.HttpPort, name: RedPandaKafkaUiContainerResource.HttpEndpointName)
            .WithEnvironment(context => ConfigureKafkaUiContainer(context, builder.Resource))
            .WaitFor(builder)
            // Nest the Kafka UI under the Redpanda resource in the dashboard so the management UIs
            // appear as children of the broker they belong to.
            .WithParentRelationship(builder)
            .ExcludeFromManifest();

        configureContainer?.Invoke(kafkaUiBuilder);

        return builder;
    }

    /// <summary>
    /// Configures the host port that the Kafka UI resource is exposed on instead of using a randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder for the Kafka UI.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> a random port is assigned.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExportIgnore(Reason = "The exported 'withHostPort' capability is already provided by the RedPandaConsoleContainerResource overload; this overload remains available to C# callers.")]
    public static IResourceBuilder<RedPandaKafkaUiContainerResource> WithHostPort(
        this IResourceBuilder<RedPandaKafkaUiContainerResource> builder,
        int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(RedPandaKafkaUiContainerResource.HttpEndpointName, endpoint => endpoint.Port = port);
    }

    private static void ConfigureRedPandaArgs(CommandLineArgsCallbackContext context, RedPandaServerResource resource, RedPandaServerOptions options)
    {
        // Start a single-node Redpanda broker tuned for local development.
        // See https://docs.redpanda.com/current/reference/rpk/rpk-redpanda/rpk-redpanda-start/
        context.Args.Add("redpanda");
        context.Args.Add("start");
        context.Args.Add("--mode");
        context.Args.Add("dev-container");
        context.Args.Add("--smp");
        context.Args.Add(options.CpuCount.ToString(CultureInfo.InvariantCulture));
        context.Args.Add("--memory");
        context.Args.Add(options.Memory);

        // Two Kafka listeners: an "internal" listener for container-to-container traffic over the
        // Aspire container network, and an "external" listener that is reachable from the host.
        context.Args.Add("--kafka-addr");
        context.Args.Add($"internal://0.0.0.0:{RedPandaServerResource.KafkaInternalBrokerPort},external://0.0.0.0:{RedPandaServerResource.KafkaBrokerPort}");

        // Advertised listeners tell clients how to reach the broker after the initial connection.
        var internalPort = RedPandaServerResource.KafkaInternalBrokerPort.ToString(CultureInfo.InvariantCulture);
        var advertised = context.ExecutionContext.IsRunMode
            // In run mode, the internal listener is reached over the default Aspire container network using
            // the resource name, and the external listener is reached from the host on the mapped port.
            ? ReferenceExpression.Create(
                $"internal://{resource.Name}:{internalPort},external://localhost:{resource.PrimaryEndpoint.Property(EndpointProperty.Port)}")
            : ReferenceExpression.Create(
                $"internal://{resource.InternalEndpoint.Property(EndpointProperty.HostAndPort)},external://{resource.PrimaryEndpoint.Property(EndpointProperty.HostAndPort)}");

        context.Args.Add("--advertise-kafka-addr");
        context.Args.Add(advertised);

        context.Args.Add("--schema-registry-addr");
        context.Args.Add($"0.0.0.0:{RedPandaServerResource.SchemaRegistryPort}");
    }

    private static void ConfigureConsoleContainer(EnvironmentCallbackContext context, RedPandaServerResource resource)
    {
        // The console runs in its own container, so it reaches Redpanda over the default Aspire container
        // network in run mode (using the resource name + target ports) and over the host otherwise.
        var brokers = context.ExecutionContext.IsRunMode
            ? ReferenceExpression.Create($"{resource.Name}:{resource.InternalEndpoint.Property(EndpointProperty.TargetPort)}")
            : ReferenceExpression.Create($"{resource.InternalEndpoint.Property(EndpointProperty.HostAndPort)}");

        var schemaRegistry = context.ExecutionContext.IsRunMode
            ? ReferenceExpression.Create($"http://{resource.Name}:{resource.SchemaRegistryEndpoint.Property(EndpointProperty.TargetPort)}")
            : ReferenceExpression.Create($"{resource.SchemaRegistryEndpoint.Property(EndpointProperty.Scheme)}://{resource.SchemaRegistryEndpoint.Property(EndpointProperty.HostAndPort)}");

        var adminApi = context.ExecutionContext.IsRunMode
            ? ReferenceExpression.Create($"http://{resource.Name}:{resource.AdminEndpoint.Property(EndpointProperty.TargetPort)}")
            : ReferenceExpression.Create($"{resource.AdminEndpoint.Property(EndpointProperty.Scheme)}://{resource.AdminEndpoint.Property(EndpointProperty.HostAndPort)}");

        context.EnvironmentVariables["KAFKA_BROKERS"] = brokers;
        context.EnvironmentVariables["KAFKA_SCHEMAREGISTRY_ENABLED"] = "true";
        context.EnvironmentVariables["KAFKA_SCHEMAREGISTRY_URLS"] = schemaRegistry;
        context.EnvironmentVariables["REDPANDA_ADMINAPI_ENABLED"] = "true";
        context.EnvironmentVariables["REDPANDA_ADMINAPI_URLS"] = adminApi;
    }

    private static void ConfigureKafkaUiContainer(EnvironmentCallbackContext context, RedPandaServerResource resource)
    {
        // Kafka UI runs in its own container, so it reaches Redpanda over the default Aspire container
        // network in run mode (using the resource name + target ports) and over the host otherwise.
        var bootstrapServers = context.ExecutionContext.IsRunMode
            ? ReferenceExpression.Create($"{resource.Name}:{resource.InternalEndpoint.Property(EndpointProperty.TargetPort)}")
            : ReferenceExpression.Create($"{resource.InternalEndpoint.Property(EndpointProperty.HostAndPort)}");

        var schemaRegistry = context.ExecutionContext.IsRunMode
            ? ReferenceExpression.Create($"http://{resource.Name}:{resource.SchemaRegistryEndpoint.Property(EndpointProperty.TargetPort)}")
            : ReferenceExpression.Create($"{resource.SchemaRegistryEndpoint.Property(EndpointProperty.Scheme)}://{resource.SchemaRegistryEndpoint.Property(EndpointProperty.HostAndPort)}");

        context.EnvironmentVariables["KAFKA_CLUSTERS_0_NAME"] = resource.Name;
        context.EnvironmentVariables["KAFKA_CLUSTERS_0_BOOTSTRAPSERVERS"] = bootstrapServers;
        context.EnvironmentVariables["KAFKA_CLUSTERS_0_SCHEMAREGISTRY"] = schemaRegistry;
    }
}
