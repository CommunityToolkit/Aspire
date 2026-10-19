using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Loki;

#pragma warning disable ASPIREATS001
#pragma warning disable ASPIRECERTIFICATES001

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Grafana Loki resources.
/// </summary>
public static class LokiBuilderExtensions
{
    /// <summary>
    /// Adds a Grafana Loki container to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The Loki resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<LokiResource> AddLoki(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var resourceBuilder = builder.AddResource(new LokiResource(name))
            .WithImage(LokiContainerImageTags.Image, LokiContainerImageTags.Tag)
            .WithImageRegistry(LokiContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: 3100, name: LokiResource.HttpEndpointName)
            .WithEndpoint(targetPort: 9096, name: LokiResource.GrpcEndpointName, scheme: "http")
            .WithEndpoint(LokiResource.GrpcEndpointName, endpoint => endpoint.Transport = "http2")
            .WithHttpHealthCheck("/ready", endpointName: LokiResource.HttpEndpointName)
            .WithArgs("-config.file=/etc/loki/local-config.yaml")
            .WithIconName("TextBulletListSquare");

        resourceBuilder.WithHttpsCertificateConfiguration(context =>
        {
            context.Arguments.Add(ReferenceExpression.Create($"-server.http-tls-cert-path={context.CertificatePath}"));
            context.Arguments.Add(ReferenceExpression.Create($"-server.http-tls-key-path={context.KeyPath}"));
            return Task.CompletedTask;
        });

        resourceBuilder.SubscribeHttpsEndpointsUpdate(context =>
        {
            resourceBuilder.WithEndpoint(LokiResource.HttpEndpointName, endpoint => endpoint.UriScheme = "https");
        });

        return resourceBuilder;
    }

    /// <summary>
    /// Configures the HTTP endpoint's host port.
    /// </summary>
    /// <param name="builder">The Loki resource builder.</param>
    /// <param name="port">The host port, or <see langword="null"/> to assign a random port.</param>
    /// <returns>The Loki resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<LokiResource> WithHttpHostPort(this IResourceBuilder<LokiResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(LokiResource.HttpEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Configures the gRPC endpoint's host port.
    /// </summary>
    /// <param name="builder">The Loki resource builder.</param>
    /// <param name="port">The host port, or <see langword="null"/> to assign a random port.</param>
    /// <returns>The Loki resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<LokiResource> WithGrpcHostPort(this IResourceBuilder<LokiResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(LokiResource.GrpcEndpointName, endpoint => endpoint.Port = port);
    }

    /// <summary>
    /// Adds a named volume for the default Loki configuration's data directory, <c>/loki</c>.
    /// </summary>
    /// <param name="builder">The Loki resource builder.</param>
    /// <param name="name">The optional volume name. A name is generated when omitted.</param>
    /// <returns>The Loki resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<LokiResource> WithDataVolume(this IResourceBuilder<LokiResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/loki");
    }

    /// <summary>
    /// Adds a writable bind mount for the default Loki configuration's data directory, <c>/loki</c>.
    /// </summary>
    /// <param name="builder">The Loki resource builder.</param>
    /// <param name="source">The host directory. Relative paths are resolved against the AppHost directory.</param>
    /// <returns>The Loki resource builder.</returns>
    /// <remarks>The directory must be writable by the container user (UID 10001).</remarks>
    [AspireExport]
    public static IResourceBuilder<LokiResource> WithDataBindMount(this IResourceBuilder<LokiResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return builder.WithBindMount(source, "/loki");
    }

    /// <summary>
    /// Copies a Loki configuration file to <c>/etc/loki/local-config.yaml</c> for local runs and publishing.
    /// </summary>
    /// <param name="builder">The Loki resource builder.</param>
    /// <param name="source">The host configuration file. Relative paths are resolved against the AppHost directory.</param>
    /// <returns>The Loki resource builder.</returns>
    /// <remarks>Use <c>/loki</c> for storage paths when combining this method with the data mount helpers.</remarks>
    [AspireExport]
    public static IResourceBuilder<LokiResource> WithConfigFile(this IResourceBuilder<LokiResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        var path = Path.GetFullPath(source, builder.ApplicationBuilder.AppHostDirectory);
        return builder.WithContainerFiles("/etc/loki",
        [
            new ContainerFile
            {
                Name = "local-config.yaml",
                SourcePath = path
            }
        ]);
    }
}

#pragma warning restore ASPIREATS001
#pragma warning restore ASPIRECERTIFICATES001