using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Permify;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Permify to an <see cref="IDistributedApplicationBuilder" />.
/// </summary>
public static class PermifyHostingExtensions
{
    /// <summary>
    /// Adds a Permify resource to the application.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the Permify resource.</param>
    /// <param name="httpPort">The HTTP port for Permify.</param>
    /// <param name="grpcPort">The gRPC port for Permify.</param>
    public static IResourceBuilder<PermifyResource> AddPermify(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? httpPort = null,
        int? grpcPort = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var resource = new PermifyResource(name);
        var permifyBuilder = builder.AddResource(resource)
            .WithImage(PermifyContainerImageTags.Image)
            .WithImageTag(PermifyContainerImageTags.Tag)
            .WithImageRegistry(PermifyContainerImageTags.Registry)
            // Configure endpoints
            .WithHttpsEndpoint(
                targetPort: 8080,
                port: httpPort,
                name: PermifyResource.HttpEndpointName
            )
            .WithEnvironment("PERMIFY_HTTP_ENABLED", "true")
            .WithEnvironment("PERMIFY_HTTP_PORT", "8080")
            .WithHttpHealthCheck("/healthz")
            .WithHttpsEndpoint(
                targetPort: 8081,
                port: grpcPort,
                name: PermifyResource.GrpcEndpointName
            )
            .WithEnvironment("PERMIFY_GRPC_ENABLED", "true")
            .WithEnvironment("PERMIFY_GRPC_PORT", "8081")
            // Configure OTLP
            .WithOtlpExporter()
            .WithEnvironment("PERMIFY_TRACER_ENABLED", "true")
            .WithEnvironment("PERMIFY_TRACER_EXPORTER", "otlp")
            .WithEnvironment("PERMIFY_METER_ENABLED", "true")
            .WithEnvironment("PERMIFY_METER_EXPORTER", "otlp")
            .WithEnvironment(ctx =>
            {
                // TODO: Permify requires the endpoint to *just* be the host + port
                // it cannot contain a scheme, which makes it difficult to use HostUrl
                ctx.EnvironmentVariables["PERMIFY_TRACER_ENDPOINT"] = string.Empty;
                ctx.EnvironmentVariables["PERMIFY_METER_ENDPOINT"] = string.Empty;
            });

#pragma warning disable ASPIRECERTIFICATES001
        permifyBuilder.WithHttpsCertificateConfiguration(ctx =>
        {
            // Configure HTTPS
            ctx.EnvironmentVariables["PERMIFY_HTTP_TLS_ENABLED"] = true;
            ctx.EnvironmentVariables["PERMIFY_HTTP_TLS_CERT_PATH"] = ctx.CertificatePath;
            ctx.EnvironmentVariables["PERMIFY_HTTP_TLS_KEY_PATH"] = ctx.KeyPath;

            // Configure gRPC
            ctx.EnvironmentVariables["PERMIFY_GRPC_TLS_ENABLED"] = true;
            ctx.EnvironmentVariables["PERMIFY_GRPC_TLS_CERT_PATH"] = ctx.CertificatePath;
            ctx.EnvironmentVariables["PERMIFY_GRPC_TLS_KEY_PATH"] = ctx.KeyPath;

            return Task.CompletedTask;
        });
#pragma warning restore ASPIRECERTIFICATES001

        return permifyBuilder;
    }
}