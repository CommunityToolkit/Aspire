// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.k6;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Grafana k6 resources to the application model.
/// </summary>
public static class K6BuilderExtensions
{
    private const int K6Port = 6565;

    /// <summary>
    /// Adds a Grafana k6 container resource to the application model.
    /// The default image is <inheritdoc cref="K6ContainerImageTags.Image"/> and the tag is <inheritdoc cref="K6ContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="enableBrowserExtensions">Enables browser automation and end-to-end web testing to k6. <see href="https://grafana.com/docs/k6/latest/using-k6-browser/"/></param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an Grafana k6 container to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    /// 
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    /// var k6 = builder.AddK6("k6");
    ///     .WithReference(api);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<K6Resource> AddK6(
        this IDistributedApplicationBuilder builder,
        string name,
        bool enableBrowserExtensions = false,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var k6Resource = new K6Resource(name);
        
        string tag = enableBrowserExtensions
            ? $"{K6ContainerImageTags.Tag}-with-browser"
            : K6ContainerImageTags.Tag;

        return builder.AddResource(k6Resource)
            .WithImage(K6ContainerImageTags.Image, tag)
            .WithImageRegistry(K6ContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: K6Port, port: port, name: K6Resource.PrimaryEndpointName)
            .WithHttpHealthCheck("/health")
            .WithOtlpExporter();

    }

    /// <summary>
    /// Runs a k6 JS script when starting the Grafana k6 container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="scriptPath">The path to the JS script to run.</param>
    /// <param name="virtualUsers">The number of virtual users for the test. Defaults to `10`.</param>
    /// <param name="duration">The duration of the test. Defaults to `30s`.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a Grafana k6 container to the application model and reference it in a .NET project. Additionally, in this
    /// example a script runs when the container starts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api");
    /// var k6 = builder.AddK6("k6")
    ///     .WithBindMount("scripts", "/scripts", true)
    ///     .WithScript("/scripts/main.js")
    ///     .WithReference(api)
    ///     .WaitFor(api);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<K6Resource> WithScript(
        this IResourceBuilder<K6Resource> builder,
        string scriptPath,
        int virtualUsers = 10,
        string duration = "30s")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(scriptPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(virtualUsers);
        ArgumentNullException.ThrowIfNull(duration);

        return builder.WithArgs(
            "run", 
            "--address",
            $"0.0.0.0:{K6Port}",
            "--vus", 
            virtualUsers, 
            "--duration", 
            duration, 
            scriptPath);
    }

    /// <summary>
    /// Set K6 environment variables from the existing OTEL environment set for this resource.
    /// See https://grafana.com/docs/k6/latest/results-output/real-time/opentelemetry/#configuration.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<K6Resource> WithK6OtlpEnvironment(
        this IResourceBuilder<K6Resource> builder)
    {
        return builder.WithEnvironment(context =>
        {
            foreach (var (key, value) in context.EnvironmentVariables.ToList())
            {
                if (key.StartsWith("OTEL_"))
                {
                    context.EnvironmentVariables.TryAdd($"K6_{key}", value);
                }
            }
        });
    }
}