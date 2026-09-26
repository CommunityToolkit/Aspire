// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.k6;
using System.Globalization;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

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
    [AspireExport]
    public static IResourceBuilder<K6Resource> AddK6(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
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
            .WithOtlpExporter()
            .WithIconName("TopSpeed");

    }

    /// <summary>
    /// Runs a k6 JS script when starting the Grafana k6 container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="scriptPath">The path to the JS script to run.</param>
    /// <param name="virtualUsers">The number of virtual users for the test..</param>
    /// <param name="duration">The duration of the test, e.g. <c>30s</c>.</param>
    /// <param name="summaryMode">The summary mode of the test execution.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <c>--vus</c> and <c>--duration</c> are the k6 CLI shorthand for a single constant-VU scenario and take precedence over the script's <c>options.scenarios</c>.
    /// Leave them <see langword="null"/> when the script defines its own scenarios or executors.
    /// <example>
    /// Add a Grafana k6 container to the application model and reference it in a .NET project.
    /// Additionally, in this example a script runs when the container starts.
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
    [AspireExport]
    public static IResourceBuilder<K6Resource> WithScript(
        this IResourceBuilder<K6Resource> builder,
        string scriptPath,
        int? virtualUsers = null,
        string? duration = null,
        K6SummaryMode? summaryMode = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(scriptPath);

        var args = new List<string> { "run", "--address", $"0.0.0.0:{K6Port}" };

        if (virtualUsers is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(virtualUsers.Value, nameof(virtualUsers));
            args.Add("--vus");
            args.Add(virtualUsers.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (duration is not null)
        {
            args.Add("--duration");
            args.Add(duration);
        }

        if (summaryMode is not null)
        {
            args.Add("--summary-mode");
            args.Add(summaryMode.Value.ToString().ToLowerInvariant());
        }

        args.Add(scriptPath);

        return builder.WithArgs([.. args]);
    }

    /// <summary>
    /// Set K6 environment variables from the existing OTEL environment set for this resource.
    /// See https://grafana.com/docs/k6/latest/results-output/real-time/opentelemetry/#configuration.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
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

#pragma warning restore ASPIREATS001 // AspireExport is experimental
