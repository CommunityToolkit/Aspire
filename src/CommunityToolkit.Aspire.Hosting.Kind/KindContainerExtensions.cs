// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Kind;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREATS001 // AspireExport APIs are experimental

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for configuring container resources to work with Kind clusters.
/// </summary>
public static class KindContainerExtensions
{
    private const int RandomSuffixLength = 8;
    /// <summary>
    /// Configures a container resource to reference the Kind cluster by bind-mounting the container-compatible
    /// kubeconfig, injecting environment variables, and connecting to the Kind container network.
    /// </summary>
    /// <param name="builder">The container resource builder.</param>
    /// <param name="kind">The Kind cluster resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ContainerResource}"/>.</returns>
    /// <remarks>
    /// <para>
    /// This overload is the primary integration path for containers. It:
    /// <list type="bullet">
    /// <item>Bind-mounts the container-compatible kubeconfig into the container at <c>/etc/kubeconfig/config</c></item>
    /// <item>Sets <c>KUBECONFIG</c> to the in-container mount path</item>
    /// <item>Sets <c>K8S_CLUSTER_NAME</c> to the Kind cluster name</item>
    /// <item>Automatically connects the container to the Kind network</item>
    /// </list>
    /// </para>
    /// <para>
    /// The container-compatible kubeconfig uses the Kind control-plane container name 
    /// (<c>{clusterName}-control-plane</c>) instead of <c>127.0.0.1</c>, enabling container-to-container 
    /// communication over the Kind container network.
    /// </para>
    /// </remarks>
    [AspireExport("withKindContainerReference")]
    public static IResourceBuilder<ContainerResource> WithReference(
        this IResourceBuilder<ContainerResource> builder,
        IResourceBuilder<KindClusterResource> kind)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(kind);

        const string containerKubeconfigPath = "/etc/kubeconfig/config";

        return builder
            .WaitFor(kind)
            .WithBindMount(kind.Resource.ContainerKubeconfigPath, containerKubeconfigPath, isReadOnly: true)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["KUBECONFIG"] = containerKubeconfigPath;
                context.EnvironmentVariables["K8S_CLUSTER_NAME"] = kind.Resource.Name;
            })
            .WithKindNetwork();
    }

    /// <summary>
    /// Connects a container resource to the Kind container network, enabling it to
    /// communicate with the Kind cluster's API server and nodes.
    /// </summary>
    /// <param name="builder">The container resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ContainerResource}"/>.</returns>
    /// <remarks>
    /// <para>
    /// Use this method when a container needs direct network connectivity to the Kind control plane.
    /// </para>
    /// <para>
    /// Kind creates a separate container network ("kind") for its nodes. Aspire manages
    /// its own container network for containers it creates. Containers on different
    /// networks cannot communicate by default.
    /// </para>
    /// <para>
    /// Aspire does not provide a built-in way to add containers to additional container networks,
    /// and <c>WithContainerRuntimeArgs("--network", "kind")</c> is overridden internally.
    /// See <see href="https://github.com/microsoft/aspire/issues/14081"/> for the upstream feature request.
    /// </para>
    /// <para>
    /// This method works around the limitation by subscribing to the container's stopped event.
    /// When the container stops (typically because it can't reach the Kind cluster), the hook
    /// connects the container to the "kind" network via the configured container runtime and
    /// restarts it.
    /// </para>
    /// <para>
    /// If the container does not already have an explicit name (via <c>WithContainerName</c>),
    /// this method assigns one using the pattern <c>{resource.Name}-{random}</c> to ensure
    /// the container can be identified by the handler.
    /// </para>
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<ContainerResource> WithKindNetwork(
        this IResourceBuilder<ContainerResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        EnsureContainerName(builder);

        // For containers that start successfully but need the Kind network
        builder.ApplicationBuilder.Eventing
            .Subscribe<ResourceReadyEvent>(builder.Resource, (e, ct) =>
                EnsureConnectedToKindNetworkAsync(e.Resource, e.Services, ct));

        // For containers that crash on startup because they can't reach the Kind API.
        builder.ApplicationBuilder.Eventing
            .Subscribe<ResourceStoppedEvent>(builder.Resource, async (e, ct) =>
            {
                if (!await EnsureConnectedToKindNetworkAsync(e.Resource, e.Services, ct).ConfigureAwait(false))
                {
                    return;
                }

                var containerName = e.Resource.Annotations
                    .OfType<ContainerNameAnnotation>()
                    .Single()
                    .Name;

                var loggerService = e.Services.GetRequiredService<ResourceLoggerService>();
                var logger = loggerService.GetLogger(containerName);
                var processRunner = e.Services.GetRequiredService<IProcessRunner>();
                var containerRuntimeResolver = e.Services.GetRequiredService<IKindContainerRuntimeResolver>();
                var containerRuntime = await containerRuntimeResolver.ResolveAsync(ct).ConfigureAwait(false);
                var startResult = await processRunner.RunAsync(
                    logger,
                    containerRuntime.Executable,
                    ["start", containerName],
                    cancellationToken: ct).ConfigureAwait(false);

                if (startResult.ExitCode != 0)
                {
                    logger.LogError(
                        "Failed to restart container '{ContainerName}' after connecting to 'kind' network: {Error}",
                        containerName, startResult.Error);
                }
            });

        return builder;
    }

    /// <summary>
    /// Connects the container to the "kind" container network.
    /// </summary>
    /// <returns><see langword="true"/> if a new connection was made; otherwise, <see langword="false"/>.</returns>
    private static async Task<bool> EnsureConnectedToKindNetworkAsync(
        IResource resource, IServiceProvider services, CancellationToken ct)
    {
        var containerName = resource.Annotations
            .OfType<ContainerNameAnnotation>()
            .Single()
            .Name;

        var loggerService = services.GetRequiredService<ResourceLoggerService>();
        var logger = loggerService.GetLogger(containerName);
        var processRunner = services.GetRequiredService<IProcessRunner>();
        var containerRuntimeResolver = services.GetRequiredService<IKindContainerRuntimeResolver>();
        var containerRuntime = await containerRuntimeResolver.ResolveAsync(ct).ConfigureAwait(false);

        var connectResult = await processRunner.RunAsync(
            logger,
            containerRuntime.Executable,
            [
                "network",
                "connect",
                "kind",
                containerName,
            ],
            cancellationToken: ct).ConfigureAwait(false);

        if (connectResult.ExitCode != 0)
        {
            // Docker and Podman each report a distinct error when the container is already
            // connected to the network. No new connection means a stopped container should stay stopped.
            if (IsAlreadyConnectedError(connectResult))
            {
                return false;
            }

            logger.LogError(
                "Failed to connect container '{ContainerName}' to 'kind' network: {Error}",
                containerName, connectResult.Error);
            return false;
        }

        return true;
    }

    private static bool IsAlreadyConnectedError(ProcessResult connectResult)
    {
        // Docker reports "endpoint ... already exists in network kind" and Podman reports
        // "... is already connected to network kind" when the container is already attached.
        // Either message means the container is already on the Kind network, which is success.
        return connectResult.Error.Contains("already exists in network", StringComparison.OrdinalIgnoreCase) ||
            connectResult.Error.Contains("already connected to network", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Assigns a container name if one has not already been set via <c>WithContainerName</c>.
    /// Mirrors the DCP naming convention: <c>{resource.Name}-{random 8-char suffix}</c>.
    /// </summary>
    private static void EnsureContainerName(IResourceBuilder<ContainerResource> builder)
    {
        if (builder.Resource.TryGetLastAnnotation<ContainerNameAnnotation>(out _))
        {
            return;
        }

        var suffix = GenerateRandomSuffix(RandomSuffixLength);
        builder.WithContainerName($"{builder.Resource.Name}-{suffix}");
    }

    private static string GenerateRandomSuffix(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz";
        return RandomNumberGenerator.GetString(chars, length);
    }
}

#pragma warning restore ASPIREATS001
