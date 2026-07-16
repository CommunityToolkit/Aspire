// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Manages the lifecycle of a Kind cluster by orchestrating CLI calls.
/// </summary>
internal sealed class KindClusterManager
{
    private readonly IKindResource _resource;
    private readonly ILogger _logger;
    private readonly IProcessRunner _processRunner;
    private readonly IKindContainerRuntimeResolver _containerRuntimeResolver;
    private KindContainerRuntime? _containerRuntime;

    public KindClusterManager(
        IKindResource resource,
        ILogger logger,
        IProcessRunner processRunner,
        IKindContainerRuntimeResolver containerRuntimeResolver)
    {
        _resource = resource;
        _logger = logger;
        _processRunner = processRunner;
        _containerRuntimeResolver = containerRuntimeResolver;
    }

    /// <summary>
    /// Creates the Kind cluster, reusing an existing running cluster if found.
    /// </summary>
    public async Task CreateClusterAsync(CancellationToken cancellationToken)
    {
        var containerRuntime = await GetContainerRuntimeAsync(cancellationToken).ConfigureAwait(false);

        if (await IsControlPlaneRunningAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Kind cluster '{ClusterName}' is already running, exporting kubeconfig.", _resource.Name);
            await ExportKubeconfigAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // A stale cluster (containers stopped) must be deleted before re-creation.
        if (await ClusterExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Deleting stale Kind cluster '{ClusterName}'.", _resource.Name);
            await DeleteClusterAsync(cancellationToken).ConfigureAwait(false);
        }

        var configPath = await KindConfigGenerator.GenerateConfigAsync(_resource, cancellationToken).ConfigureAwait(false);

        try
        {
            _logger.LogInformation("Creating Kind cluster '{ClusterName}'...", _resource.Name);
            EnsureKubeconfigDirectoryExists();
            var result = await _processRunner.RunAsync(
                _logger,
                "kind",
                [
                    "create",
                    "cluster",
                    $"--name={_resource.Name}",
                    $"--config={configPath}",
                    $"--kubeconfig={_resource.KubeconfigPath}",
                ],
                environmentVariables: containerRuntime.KindEnvironmentVariables,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to create Kind cluster '{_resource.Name}': {result.Error}");
            }

            _logger.LogInformation("Kind cluster '{ClusterName}' created successfully.", _resource.Name);
        }
        finally
        {
            // Clean up temp config file.
            try { File.Delete(configPath); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Deletes the Kind cluster.
    /// </summary>
    public async Task DeleteClusterAsync(CancellationToken cancellationToken)
    {
        var containerRuntime = await GetContainerRuntimeAsync(cancellationToken).ConfigureAwait(false);

        var result = await _processRunner.RunAsync(
            _logger,
            "kind",
            [
                "delete",
                "cluster",
                $"--name={_resource.Name}",
            ],
            environmentVariables: containerRuntime.KindEnvironmentVariables,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to delete Kind cluster '{_resource.Name}': {result.Error}");
        }
    }

    /// <summary>
    /// Checks if the Kind control-plane container is running.
    /// </summary>
    public async Task<bool> IsControlPlaneRunningAsync(CancellationToken cancellationToken)
    {
        var containerRuntime = await GetContainerRuntimeAsync(cancellationToken).ConfigureAwait(false);

        var containerName = $"{_resource.Name}-control-plane";
        var result = await _processRunner.RunAsync(
            _logger,
            containerRuntime.Executable,
            [
                "ps",
                "--filter",
                $"name={containerName}",
                "--filter",
                "status=running",
                "--format",
                "{{.Names}}",
            ],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Container runtime command failed: {result.Error}");
        }

        return result.Output.Contains(containerName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> ClusterExistsAsync(CancellationToken cancellationToken)
    {
        var containerRuntime = await GetContainerRuntimeAsync(cancellationToken).ConfigureAwait(false);

        var result = await _processRunner.RunAsync(
            _logger,
            "kind",
            [
                "get",
                "clusters",
            ],
            environmentVariables: containerRuntime.KindEnvironmentVariables,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Kind CLI failed: {result.Error}");
        }

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.Trim().Equals(_resource.Name, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ExportKubeconfigAsync(CancellationToken cancellationToken)
    {
        var containerRuntime = await GetContainerRuntimeAsync(cancellationToken).ConfigureAwait(false);

        EnsureKubeconfigDirectoryExists();

        var result = await _processRunner.RunAsync(
            _logger,
            "kind",
            [
                "export",
                "kubeconfig",
                $"--name={_resource.Name}",
                $"--kubeconfig={_resource.KubeconfigPath}",
            ],
            environmentVariables: containerRuntime.KindEnvironmentVariables,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to export kubeconfig for '{_resource.Name}': {result.Error}");
        }
    }

    /// <summary>
    /// Ensures the parent directory of the kubeconfig path exists before the Kind CLI writes to it.
    /// </summary>
    private void EnsureKubeconfigDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_resource.KubeconfigPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private async Task<KindContainerRuntime> GetContainerRuntimeAsync(CancellationToken cancellationToken)
    {
        if (_containerRuntime is not null)
        {
            return _containerRuntime;
        }

        _containerRuntime = await _containerRuntimeResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        return _containerRuntime;
    }
}
