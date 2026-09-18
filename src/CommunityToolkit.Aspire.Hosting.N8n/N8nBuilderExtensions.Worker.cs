using Aspire.Hosting.ApplicationModel;
using Zemires.Aspire.Hosting.N8n;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding N8n resources to the application model.
/// </summary>
public static partial class N8nBuilderExtensions
{
    /// <summary>
    /// Adds a worker instance for the given N8n resource.
    /// Worker instances run the n8n process in "worker" mode and are configured to
    /// share the same encryption key and webhook configuration as the primary N8n resource.
    /// The worker is created as a child of the main N8n resource so lifecycle and ordering
    /// are handled automatically.
    /// </summary>
    /// <param name="n8nBuilder">The primary N8n resource builder to attach the worker to.</param>
    /// <param name="name">The name to use for the worker resource.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <returns>A new <see cref="IResourceBuilder{T}"/> for the <see cref="N8nWorkerResource"/> instance.</returns>
    [AspireExport]
    public static IResourceBuilder<N8nWorkerResource> AddWorker(this IResourceBuilder<N8nResource> n8nBuilder, string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(n8nBuilder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        // worker does not support https
        var worker = new N8nWorkerResource(n8nBuilder.Resource.Name + "-" + name, n8nBuilder.Resource);

        var workerBuilder = n8nBuilder.ApplicationBuilder.AddResource(worker)
            .WithAnnotation(new ContainerImageAnnotation { Image = N8nContainerImageTags.Image, Tag = N8nContainerImageTags.Tag, Registry = N8nContainerImageTags.Registry })
            .WithArgs("worker")
            .WithIconName("SettingsCogMultiple", IconVariant.Filled)
            .WithHttpEndpoint(targetPort: N8nPort, port: port, name: N8nResource.PrimaryEndpointName, env: "N8N_PORT")
            .WithHttpHealthCheck("/healthz", 200, N8nResource.PrimaryEndpointName)
            .WithEnvironment("N8N_ENFORCE_SETTINGS_FILE_PERMISSIONS", "false")
            .WithEnvironment("N8N_ENCRYPTION_KEY", n8nBuilder.Resource.EncryptionKeyParameter)
            .WithEnvironment("N8N_WEBHOOK_URL", n8nBuilder.GetEndpoint(N8nResource.PrimaryEndpointName, n8nBuilder.ApplicationBuilder.ExecutionContext.IsPublishMode ? KnownNetworkIdentifiers.PublicInternet : KnownNetworkIdentifiers.LocalhostNetwork))
            .WithEnvironment("QUEUE_HEALTH_CHECK_ACTIVE", "true")
            .WithParentRelationship(n8nBuilder);

        if (n8nBuilder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            // to accept dev certificate of redis in run mode
#pragma warning disable ASPIRECERTIFICATES001
            workerBuilder.WithHttpsCertificateConfiguration(ctx =>
            {
                ctx.EnvironmentVariables["NODE_EXTRA_CA_CERTS"] = ctx.CertificatePath; 
                return Task.CompletedTask;
            });
#pragma warning restore ASPIRECERTIFICATES001
        }

        return workerBuilder;
    }

    /// <summary>
    /// Configures the N8n resource to run in queue mode using a Redis instance.
    /// This sets the necessary environment variables (host, port, password and TLS) from
    /// the provided <paramref name="redis"/> resource and creates a parent/reference
    /// relationship so containers start in the correct order.
    /// </summary>
    /// <param name="builder">The N8n resource builder to configure.</param>
    /// <param name="redis">A resource builder for the Redis instance. Must expose connection string information.</param>
    /// <returns>The same <see cref="IResourceBuilder{N8nResource}"/> instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided <paramref name="redis"/> does not expose connection string information.</exception>
    [AspireExport]
    public static IResourceBuilder<N8nResource> WithQueueMode(this IResourceBuilder<N8nResource> builder, IResourceBuilder<IResource> redis)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(redis);

        if (redis.Resource is IResourceWithConnectionString resourceWithConnection)
        {
            return builder.WithEnvironment("EXECUTIONS_MODE", "queue")
                .WithEnvironment("QUEUE_BULL_REDIS_HOST", $"{resourceWithConnection.GetConnectionProperty("Host")}")
                .WithEnvironment("QUEUE_BULL_REDIS_PORT", $"{resourceWithConnection.GetConnectionProperty("Port")}")
                .WithEnvironment("QUEUE_BULL_REDIS_PASSWORD", $"{resourceWithConnection.GetConnectionProperty("Password")}")
                .WithEnvironment("QUEUE_BULL_REDIS_TLS", "true")
                .WithReferenceRelationship(redis)
                .WaitFor(redis);
        }
        else
        {
            throw new ArgumentException($"The provided resource '{redis.Resource.Name}' does not contain connection string information and cannot be used as a redis for N8n.", nameof(redis));
        }
    }
}