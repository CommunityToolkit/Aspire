using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using CommunityToolkit.Aspire.Hosting.Dapr;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides polyglot helper methods for Azure Managed Redis Dapr integrations.
/// </summary>
internal static class AzureRedisCacheDaprHostingPolyglotExtensions
{
    /// <summary>
    /// Adds an Azure Managed Redis resource for Dapr integration scenarios.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The Azure Managed Redis resource name.</param>
    /// <param name="useAccessKeyAuthentication"><see langword="true"/> to configure access key authentication.</param>
    /// <param name="runAsContainer"><see langword="true"/> to run the Redis resource locally in a container.</param>
    /// <returns>The Azure Managed Redis resource builder.</returns>
    /// <remarks>
    /// This polyglot helper bridges the underlying Azure Managed Redis factory and configuration methods
    /// so TypeScript app hosts can create resources to pass to <see cref="AzureRedisCacheDaprHostingExtensions.WithReference(IResourceBuilder{IDaprComponentResource}, IResourceBuilder{AzureManagedRedisResource})"/>.
    /// </remarks>
    [AspireExport("addAzureManagedRedisForDapr", Description = "Adds an Azure Managed Redis resource for Dapr integration")]
    internal static IResourceBuilder<AzureManagedRedisResource> AddAzureManagedRedisForDapr(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        bool useAccessKeyAuthentication = false,
        bool runAsContainer = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        IResourceBuilder<AzureManagedRedisResource> redis = builder.AddAzureManagedRedis(name);

        if (useAccessKeyAuthentication)
        {
            redis = redis.WithAccessKeyAuthentication();
        }

        if (runAsContainer)
        {
            redis = redis.RunAsContainer();
        }

        return redis;
    }

    /// <summary>
    /// Adds a Dapr state store component for Azure Managed Redis integration.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The Dapr component name.</param>
    /// <returns>The Dapr component resource builder.</returns>
    [AspireExport("addDaprStateStoreForAzureManagedRedis", Description = "Adds a Dapr state store component for Azure Managed Redis integration")]
    internal static IResourceBuilder<IDaprComponentResource> AddDaprStateStoreForAzureManagedRedis(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return builder.AddDaprStateStore(name);
    }

    /// <summary>
    /// Adds a Dapr pub/sub component for Azure Managed Redis integration.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The Dapr component name.</param>
    /// <returns>The Dapr component resource builder.</returns>
    [AspireExport("addDaprPubSubForAzureManagedRedis", Description = "Adds a Dapr pub/sub component for Azure Managed Redis integration")]
    internal static IResourceBuilder<IDaprComponentResource> AddDaprPubSubForAzureManagedRedis(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return builder.AddDaprPubSub(name);
    }
}
