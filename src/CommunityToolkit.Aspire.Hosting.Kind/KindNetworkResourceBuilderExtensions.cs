// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Kind;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for connecting container resources to the Kind Docker network.
/// </summary>
public static class KindNetworkResourceBuilderExtensions
{
    /// <summary>
    /// Connects a container resource to the Kind Docker network, enabling it to
    /// communicate with the Kind cluster's API server and nodes.
    /// </summary>
    /// <param name="builder">The container resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ContainerResource}"/>.</returns>
    /// <remarks>
    /// <para>
    /// Kind creates a separate Docker network ("kind") for its nodes. Aspire manages
    /// its own Docker network for containers it creates. Containers on different Docker
    /// networks cannot communicate by default.
    /// </para>
    /// <para>
    /// Aspire does not provide a built-in way to add containers to additional Docker networks,
    /// and <c>WithContainerRuntimeArgs("--network", "kind")</c> is overridden internally.
    /// </para>
    /// <para>
    /// This method works around the limitation by subscribing to the container's stopped event.
    /// When the container stops (typically because it can't reach the Kind cluster), the hook
    /// connects the container to the "kind" network via <c>docker network connect</c> and
    /// restarts it.
    /// </para>
    /// </remarks>
    public static IResourceBuilder<ContainerResource> WithKindNetwork(
        this IResourceBuilder<ContainerResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ApplicationBuilder.Eventing
            .Subscribe<ResourceStoppedEvent>(builder.Resource, async (e, ct) =>
            {
                if (e.Resource.TryGetLastAnnotation<KindNetworkConnectedAnnotation>(out _))
                {
                    return;
                }

                var containerName = e.Resource.Annotations
                    .OfType<ContainerNameAnnotation>()
                    .Single()
                    .Name;

                var loggerService = e.Services.GetRequiredService<ResourceLoggerService>();
                var logger = loggerService.GetLogger(containerName);

                var connectResult = await ProcessHelper.RunAsync(
                    logger,
                    "docker",
                    [
                        "network",
                        "connect",
                        "kind",
                        containerName,
                    ],
                    cancellationToken: ct).ConfigureAwait(false);

                if (connectResult.ExitCode != 0)
                {
                    logger.LogError(
                        "Failed to connect container '{ContainerName}' to 'kind' network: {Error}",
                        containerName, connectResult.Error);
                    return;
                }

                var startResult = await ProcessHelper.RunAsync(
                    logger,
                    "docker",
                    [
                        "start",
                        containerName,
                    ],
                    cancellationToken: ct).ConfigureAwait(false);

                if (startResult.ExitCode != 0)
                {
                    logger.LogError(
                        "Failed to restart container '{ContainerName}' after connecting to 'kind' network: {Error}",
                        containerName, startResult.Error);
                    return;
                }

                e.Resource.Annotations.Add(new KindNetworkConnectedAnnotation());
            });

        return builder;
    }
}
