// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.GoFeatureFlag;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding GO Feature Flag resources to the application model.
/// </summary>
public static class GoFeatureFlagBuilderExtensions
{
    private const int GoFeatureFlagPort = 1031;

    /// <summary>
    /// Adds an GO Feature Flag container resource to the application model.
    /// The default image is <inheritdoc cref="GoFeatureFlagContainerImageTags.Image"/> and the tag is <inheritdoc cref="GoFeatureFlagContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="pathToConfigFile">The path set to find the configuration file (https://gofeatureflag.org/docs/relay-proxy/configure-relay-proxy#configuration-file).</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an GO Feature Flag container to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    /// 
    /// var goff = builder.AddGoFeatureFlag("goff");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(goff);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<GoFeatureFlagResource> AddGoFeatureFlag(
        this IDistributedApplicationBuilder builder,
        string name,
        string? pathToConfigFile = null,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var goFeatureFlagResource = new GoFeatureFlagResource(name);

        string[] args = string.IsNullOrWhiteSpace(pathToConfigFile)
            ? []
            : [$"--config={pathToConfigFile}"];

        return builder.AddResource(goFeatureFlagResource)
            .WithImage(GoFeatureFlagContainerImageTags.Image, GoFeatureFlagContainerImageTags.Tag)
            .WithImageRegistry(GoFeatureFlagContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: GoFeatureFlagPort, port: port, name: GoFeatureFlagResource.PrimaryEndpointName)
            .WithHttpHealthCheck("/health")
            .WithEntrypoint("/go-feature-flag")
            .WithArgs(args)
            .WithOtlpExporter();
    }

    /// <summary>
    /// Adds a named volume for the data folder to a GO Feature flag container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a GO Feature flag container to the application model and reference it in a .NET project. Additionally, in this
    /// example a data volume is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var goff = builder.AddGoFeatureFlag("goff")
    /// .WithDataVolume();
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(goff);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<GoFeatureFlagResource> WithDataVolume(this IResourceBuilder<GoFeatureFlagResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/goff_data");
    }

    /// <summary>
    /// Adds a bind mount for the goff configuration folder to a GO Feature flag container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a GO Feature flag container to the application model and reference it in a .NET project. Additionally, in this
    /// example a bind mount is added to the container to allow reading goff configuration.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var goff = builder.AddGoFeatureFlag("goff")
    /// .WithGoffBindMount("./goff");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(goff);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<GoFeatureFlagResource> WithGoffBindMount(this IResourceBuilder<GoFeatureFlagResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/goff");
    }
}