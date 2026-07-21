// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Handles cleanup of Kind clusters on application shutdown.
/// Clusters with <see cref="ClusterLifetime.Session"/> lifetime are deleted;
/// clusters with <see cref="ClusterLifetime.Persistent"/> lifetime are left running.
/// </summary>
internal sealed class KindClusterLifecycleHook(
    DistributedApplicationModel appModel,
    ResourceLoggerService loggerService,
    IProcessRunner processRunner,
    IKindContainerRuntimeResolver containerRuntimeResolver) : IDistributedApplicationEventingSubscriber, IAsyncDisposable
{
    /// <inheritdoc />
    public Task SubscribeAsync(
        IDistributedApplicationEventing eventing,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var clusters = appModel.Resources.OfType<KindClusterResource>();

        foreach (var resource in clusters)
        {
            var lifetime = resource.TryGetLastAnnotation<ClusterLifetimeAnnotation>(out var annotation)
                ? annotation.Lifetime
                : ClusterLifetime.Session;

            if (lifetime == ClusterLifetime.Persistent)
            {
                continue;
            }

            var logger = loggerService.GetLogger(resource);
            var manager = new KindClusterManager(resource, logger, processRunner, containerRuntimeResolver);

            try
            {
                logger.LogInformation("Deleting Kind cluster '{ClusterName}' (session lifetime).", resource.Name);
                await manager.DeleteClusterAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete Kind cluster '{ClusterName}' on shutdown.", resource.Name);
            }
        }
    }
}
