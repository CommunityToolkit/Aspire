using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Tempo;

#pragma warning disable ASPIREATS001

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Grafana Tempo resources.
/// </summary>
public static class TempoBuilderExtensions
{
    private const string DefaultConfig = """
        server:
          http_listen_port: 3200
          grpc_listen_port: 9095
        distributor:
          receivers:
            otlp:
              protocols:
                grpc:
                  endpoint: 0.0.0.0:4317
                http:
                  endpoint: 0.0.0.0:4318
        storage:
          trace:
            backend: local
            wal:
                            path: /var/tempo/wal
            local:
                            path: /var/tempo/blocks
        """;

    /// <summary>
    /// Adds a Grafana Tempo container with OTLP receivers and local trace storage to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The Tempo resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TempoResource> AddTempo(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var resourceBuilder = builder.AddResource(new TempoResource(name))
            .WithImage(TempoContainerImageTags.Image, TempoContainerImageTags.Tag)
            .WithImageRegistry(TempoContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: 3200, name: TempoResource.HttpEndpointName)
            .WithEndpoint(targetPort: 9095, name: TempoResource.GrpcEndpointName, scheme: "http")
            .WithEndpoint(TempoResource.GrpcEndpointName, endpoint => endpoint.Transport = "http2")
            .WithEndpoint(targetPort: 4317, name: TempoResource.OtlpGrpcEndpointName, scheme: "http")
            .WithEndpoint(TempoResource.OtlpGrpcEndpointName, endpoint => endpoint.Transport = "http2")
            .WithHttpEndpoint(targetPort: 4318, name: TempoResource.OtlpHttpEndpointName)
            .WithHttpHealthCheck("/ready", endpointName: TempoResource.HttpEndpointName)
            .WithArgs("-config.file=/etc/tempo/tempo.yaml")
            .WithContainerFiles("/etc/tempo", (context, cancellationToken) =>
            {
                var file = new ContainerFile { Name = "tempo.yaml" };
                if (context.Model.TryGetLastAnnotation<TempoConfigFileAnnotation>(out var config))
                {
                    file.SourcePath = config.Source;
                }
                else
                {
                    file.Contents = DefaultConfig;
                }
                return Task.FromResult<IEnumerable<ContainerFileSystemItem>>([file]);
            })
            .WithIconName("Timeline");

        return resourceBuilder;
    }

    /// <summary>
    /// Configures the HTTP query endpoint's host port.
    /// </summary>
    /// <param name="builder">The Tempo resource builder.</param>
    /// <param name="port">The host port, or <see langword="null"/> to assign a random port.</param>
    /// <returns>The Tempo resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TempoResource> WithHttpHostPort(this IResourceBuilder<TempoResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEndpoint(TempoResource.HttpEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Configures the internal gRPC endpoint's host port.
    /// </summary>
    /// <param name="builder">The Tempo resource builder.</param>
    /// <param name="port">The host port, or <see langword="null"/> to assign a random port.</param>
    /// <returns>The Tempo resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TempoResource> WithGrpcHostPort(this IResourceBuilder<TempoResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEndpoint(TempoResource.GrpcEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Configures the OTLP gRPC ingestion endpoint's host port.
    /// </summary>
    /// <param name="builder">The Tempo resource builder.</param>
    /// <param name="port">The host port, or <see langword="null"/> to assign a random port.</param>
    /// <returns>The Tempo resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TempoResource> WithOtlpGrpcHostPort(this IResourceBuilder<TempoResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEndpoint(TempoResource.OtlpGrpcEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Configures the OTLP HTTP ingestion endpoint's host port.
    /// </summary>
    /// <param name="builder">The Tempo resource builder.</param>
    /// <param name="port">The host port, or <see langword="null"/> to assign a random port.</param>
    /// <returns>The Tempo resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TempoResource> WithOtlpHttpHostPort(this IResourceBuilder<TempoResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithEndpoint(TempoResource.OtlpHttpEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Adds a named volume for the default Tempo configuration's data directory, <c>/var/tempo</c>.
    /// </summary>
    /// <param name="builder">The Tempo resource builder.</param>
    /// <param name="name">The optional volume name. A name is generated when omitted.</param>
    /// <returns>The Tempo resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<TempoResource> WithDataVolume(this IResourceBuilder<TempoResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/tempo");
    }

    /// <summary>
    /// Adds a writable bind mount for the default Tempo configuration's data directory, <c>/var/tempo</c>.
    /// </summary>
    /// <param name="builder">The Tempo resource builder.</param>
    /// <param name="source">The host directory. Relative paths are resolved against the AppHost directory.</param>
    /// <returns>The Tempo resource builder.</returns>
    /// <remarks>The directory must be writable by the container user (UID 10001).</remarks>
    [AspireExport]
    public static IResourceBuilder<TempoResource> WithDataBindMount(this IResourceBuilder<TempoResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);
        return builder.WithBindMount(source, "/var/tempo");
    }

    /// <summary>
    /// Copies a Tempo configuration file to <c>/etc/tempo/tempo.yaml</c> for local runs and publishing.
    /// </summary>
    /// <param name="builder">The Tempo resource builder.</param>
    /// <param name="source">The host configuration file. Relative paths are resolved against the AppHost directory.</param>
    /// <returns>The Tempo resource builder.</returns>
    /// <remarks>Keep storage paths under <c>/var/tempo</c> and enable OTLP listeners on ports 4317 and 4318.</remarks>
    [AspireExport]
    public static IResourceBuilder<TempoResource> WithConfigFile(this IResourceBuilder<TempoResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        var path = Path.GetFullPath(source, builder.ApplicationBuilder.AppHostDirectory);
        return builder.WithAnnotation(new TempoConfigFileAnnotation(path), ResourceAnnotationMutationBehavior.Replace);
    }

    /// <summary>
    /// Identifies the custom configuration file copied into the Tempo container.
    /// </summary>
    /// <param name="source">The absolute path to the host configuration file.</param>
    private sealed class TempoConfigFileAnnotation(string source) : IResourceAnnotation
    {
        /// <summary>
        /// Gets the absolute path to the host configuration file.
        /// </summary>
        public string Source { get; } = source;
    }
}

#pragma warning restore ASPIREATS001