// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.Loader;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Handles cleanup of Kind clusters on graceful shutdown and best-effort process-exit signals.
/// Clusters with <see cref="ClusterLifetime.Session"/> lifetime are deleted;
/// clusters with <see cref="ClusterLifetime.Persistent"/> lifetime are left running.
/// </summary>
internal sealed class KindClusterLifecycleHook(
    DistributedApplicationModel appModel,
    ResourceLoggerService loggerService,
    IProcessRunner processRunner,
    IKindContainerRuntimeResolver containerRuntimeResolver,
    IHostApplicationLifetime hostApplicationLifetime) : IDistributedApplicationEventingSubscriber, IAsyncDisposable
{
    private readonly object _cleanupLock = new();
    private readonly object _registrationLock = new();
    private Task? _cleanupTask;
    private CancellationTokenRegistration _applicationStoppingRegistration;
    private EventHandler? _processExitHandler;
    private Action<AssemblyLoadContext>? _unloadingHandler;
    private ConsoleCancelEventHandler? _cancelKeyPressHandler;
    private bool _terminationHandlersRegistered;

    /// <inheritdoc />
    public Task SubscribeAsync(
        IDistributedApplicationEventing eventing,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventing);

        RegisterTerminationHandlers();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await EnsureCleanupStarted().ConfigureAwait(false);
        }
        finally
        {
            UnregisterTerminationHandlers();
        }
    }

    private Task EnsureCleanupStarted()
    {
        lock (_cleanupLock)
        {
            _cleanupTask ??= CleanupClustersAsync();
            return _cleanupTask;
        }
    }

    private void RegisterTerminationHandlers()
    {
        lock (_registrationLock)
        {
            if (_terminationHandlersRegistered)
            {
                return;
            }

            _applicationStoppingRegistration = hostApplicationLifetime.ApplicationStopping.Register(() => _ = EnsureCleanupStarted());
            _processExitHandler ??= (_, _) => RunCleanupSynchronously();
            _unloadingHandler ??= _ => RunCleanupSynchronously();
            _cancelKeyPressHandler ??= (_, _) => RunCleanupSynchronously();
            AppDomain.CurrentDomain.ProcessExit += _processExitHandler;
            AssemblyLoadContext.Default.Unloading += _unloadingHandler;
            Console.CancelKeyPress += _cancelKeyPressHandler;
            _terminationHandlersRegistered = true;
        }
    }

    private void UnregisterTerminationHandlers()
    {
        lock (_registrationLock)
        {
            if (!_terminationHandlersRegistered)
            {
                return;
            }

            _applicationStoppingRegistration.Dispose();
            if (_processExitHandler is not null)
            {
                AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
            }

            if (_unloadingHandler is not null)
            {
                AssemblyLoadContext.Default.Unloading -= _unloadingHandler;
            }

            if (_cancelKeyPressHandler is not null)
            {
                Console.CancelKeyPress -= _cancelKeyPressHandler;
            }

            _terminationHandlersRegistered = false;
        }
    }

    private void RunCleanupSynchronously()
    {
        try
        {
            EnsureCleanupStarted().GetAwaiter().GetResult();
        }
        catch
        {
        }
    }

    private async Task CleanupClustersAsync()
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