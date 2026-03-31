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
    private readonly KindClusterResource _resource;
    private readonly ILogger _logger;
    private readonly IProcessRunner _processRunner;

    public KindClusterManager(KindClusterResource resource, ILogger logger, IProcessRunner processRunner)
    {
        _resource = resource;
        _logger = logger;
        _processRunner = processRunner;
    }

    /// <summary>
    /// Creates the Kind cluster, reusing an existing running cluster if found.
    /// </summary>
    public async Task CreateClusterAsync(CancellationToken cancellationToken)
    {
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
        var result = await _processRunner.RunAsync(
            _logger,
            "kind",
            [
                "delete",
                "cluster",
                $"--name={_resource.Name}",
            ],
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
        var containerName = $"{_resource.Name}-control-plane";
        var result = await _processRunner.RunAsync(
            _logger,
            "docker",
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
            throw new InvalidOperationException($"Docker command failed: {result.Error}");
        }

        return result.Output.Contains(containerName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> ClusterExistsAsync(CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync(
            _logger,
            "kind",
            [
                "get",
                "clusters",
            ],
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
        var result = await _processRunner.RunAsync(
            _logger,
            "kind",
            [
                "export",
                "kubeconfig",
                $"--name={_resource.Name}",
                $"--kubeconfig={_resource.KubeconfigPath}",
            ],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to export kubeconfig for '{_resource.Name}': {result.Error}");
        }
    }
}

