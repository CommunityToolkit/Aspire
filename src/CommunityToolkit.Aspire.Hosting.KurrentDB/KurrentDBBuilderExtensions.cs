// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.KurrentDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding KurrentDB resources to the application model.
/// </summary>
public static class KurrentDBBuilderExtensions
{
    private const string DataTargetFolder = "/var/lib/kurrentdb";

    /// <summary>
    /// Adds a KurrentDB resource to the application model. A container is used for local development.
    /// The default image is <inheritdoc cref="KurrentDBContainerImageTags.Image"/> and the tag is <inheritdoc cref="KurrentDBContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The port on which the KurrentDB endpoint will be exposed.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a KurrentDB container to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var kurrentdb = builder.AddKurrentDB("kurrentdb");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(kurrentdb);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<KurrentDBResource> AddKurrentDB(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var kurrentDBResource = new KurrentDBResource(name);

        string? connectionString = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(kurrentDBResource, async (@event, cancellationToken) =>
        {
            connectionString = await kurrentDBResource.ConnectionStringExpression
                .GetValueAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{kurrentDBResource.Name}' resource but the connection string was null.");
        });

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                healthCheckKey,
                sp => new KurrentDBHealthCheck(connectionString!),
                failureStatus: default,
                tags: default,
                timeout: default));

        return builder
            .AddResource(kurrentDBResource)
            .WithHttpEndpoint(port: port, targetPort: KurrentDBResource.DefaultHttpPort, name: KurrentDBResource.HttpEndpointName)
            .WithImage(KurrentDBContainerImageTags.Image, KurrentDBContainerImageTags.Tag)
            .WithImageRegistry(KurrentDBContainerImageTags.Registry)
            .WithEnvironment(ConfigureKurrentDBContainer)
            .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a KurrentDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a KurrentDB container to the application model and reference it in a .NET project. Additionally, in this
    /// example a data volume is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var kurrentdb = builder.AddKurrentDB("kurrentdb")
    ///   .WithDataVolume();
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(kurrentdb);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<KurrentDBResource> WithDataVolume(this IResourceBuilder<KurrentDBResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), DataTargetFolder);
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a KurrentDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a KurrentDB container to the application model and reference it in a .NET project. Additionally, in this
    /// example a bind mount is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var kurrentdb = builder.AddKurrentDB("kurrentdb")
    ///   .WithDataBindMount("./data/kurrentdb/data");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(kurrentdb);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<KurrentDBResource> WithDataBindMount(this IResourceBuilder<KurrentDBResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, DataTargetFolder);
    }

    private static void ConfigureKurrentDBContainer(EnvironmentCallbackContext context)
    {
        context.EnvironmentVariables.Add("KURRENTDB_CLUSTER_SIZE", "1");
        context.EnvironmentVariables.Add("KURRENTDB_RUN_PROJECTIONS", "All");
        context.EnvironmentVariables.Add("KURRENTDB_START_STANDARD_PROJECTIONS", "true");
        context.EnvironmentVariables.Add("KURRENTDB_NODE_PORT", $"{KurrentDBResource.DefaultHttpPort}");
        context.EnvironmentVariables.Add("KURRENTDB_INSECURE", "true");
    }
}
