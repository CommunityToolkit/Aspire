// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.EventStore;
using HealthChecks.EventStore.gRPC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding EventStore resources to the application model.
/// </summary>
public static class EventStoreBuilderExtensions
{
    private const string DataTargetFolder = "/var/lib/eventstore";

    /// <summary>
    /// Adds an EventStore resource to the application model. A container is used for local development.
    /// The default image is <inheritdoc cref="EventStoreContainerImageTags.Image"/> and the tag is <inheritdoc cref="EventStoreContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The port on which the EventStore endpoint will be exposed.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an EventStore container to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var eventstore = builder.AddEventStore("eventstore");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(eventstore);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<EventStoreResource> AddEventStore(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var eventStoreResource = new EventStoreResource(name);

        string? connectionString = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(eventStoreResource, async (@event, cancellationToken) =>
        {
            connectionString = await eventStoreResource.ConnectionStringExpression
                .GetValueAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{eventStoreResource.Name}' resource but the connection string was null.");
        });

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                healthCheckKey,
                sp => new EventStoreHealthCheck(connectionString!),
                failureStatus: default,
                tags: default,
                timeout: default));

        return builder
            .AddResource(eventStoreResource)
            .WithHttpEndpoint(port: port, targetPort: EventStoreResource.DefaultHttpPort, name: EventStoreResource.HttpEndpointName)
            .WithImage(EventStoreContainerImageTags.Image, EventStoreContainerImageTags.Tag)
            .WithImageRegistry(EventStoreContainerImageTags.Registry)
            .WithEnvironment(ConfigureEventStoreContainer)
            .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a EventStore container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an EventStore container to the application model and reference it in a .NET project. Additionally, in this
    /// example a data volume is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var eventstore = builder.AddEventStore("eventstore")
    ///   .WithDataVolume();
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(eventstore);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<EventStoreResource> WithDataVolume(this IResourceBuilder<EventStoreResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), DataTargetFolder);
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a EventStore container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an EventStore container to the application model and reference it in a .NET project. Additionally, in this
    /// example a bind mount is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var eventstore = builder.AddEventStore("eventstore")
    ///   .WithDataBindMount("./data/eventstore/data");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(eventstore);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<EventStoreResource> WithDataBindMount(this IResourceBuilder<EventStoreResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, DataTargetFolder);
    }

    private static void ConfigureEventStoreContainer(EnvironmentCallbackContext context)
    {
        context.EnvironmentVariables.Add("EVENTSTORE_CLUSTER_SIZE", "1");
        context.EnvironmentVariables.Add("EVENTSTORE_RUN_PROJECTIONS", "All");
        context.EnvironmentVariables.Add("EVENTSTORE_START_STANDARD_PROJECTIONS", "true");
        context.EnvironmentVariables.Add("EVENTSTORE_NODE_PORT", $"{EventStoreResource.DefaultHttpPort}");
        context.EnvironmentVariables.Add("EVENTSTORE_INSECURE", "true");
    }
}
