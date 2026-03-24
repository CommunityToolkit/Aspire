using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace Aspire.Hosting;

/// <summary>
/// Manages the Kind cluster lifecycle as a .NET Aspire application lifecycle hook:
/// <list type="bullet">
///   <item><description><b>BeforeStart:</b> creates all Kind clusters in parallel.</description></item>
///   <item><description><b>AfterResourcesCreated:</b> health-checks each cluster, then applies manifests and Helm charts.</description></item>
///   <item><description><b>BeforeStop:</b> deletes all Kind clusters (best-effort) and cleans up kubeconfig files.</description></item>
/// </list>
/// </summary>
internal sealed class KindClusterLifecycleHook : IDistributedApplicationLifecycleHook
{
    private readonly ILogger<KindClusterLifecycleHook> _logger;

    private const int MaxHealthCheckAttempts = 15;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    public KindClusterLifecycleHook(ILogger<KindClusterLifecycleHook> logger)
    {
        _logger = logger;
    }

    // ── Lifecycle entry points ────────────────────────────────────────────────

    /// <summary>Creates all Kind clusters in parallel before the application starts.</summary>
    public async Task BeforeStartAsync(
        DistributedApplicationModel appModel,
        CancellationToken cancellationToken = default)
    {
        var clusters = appModel.Resources.OfType<KindClusterResource>().ToList();
        if (clusters.Count == 0) return;

        _logger.LogInformation("Creating {Count} Kind cluster(s)...", clusters.Count);

        // Parallel creation — each cluster is independent.
        await Task.WhenAll(clusters.Select(c => CreateClusterAsync(c, cancellationToken)));
    }

    /// <summary>
    /// Health-checks all clusters after resources are created, then applies manifests and Helm charts.
    /// </summary>
    public async Task AfterResourcesCreatedAsync(
        DistributedApplicationModel appModel,
        CancellationToken cancellationToken = default)
    {
        var clusters = appModel.Resources.OfType<KindClusterResource>().ToList();
        if (clusters.Count == 0) return;

        await Task.WhenAll(clusters.Select(c => WaitForReadyThenPostDeployAsync(c, cancellationToken)));
    }

    /// <summary>Deletes all Kind clusters and removes kubeconfig files before the application stops.</summary>
    public async Task BeforeStopAsync(
        DistributedApplicationModel appModel,
        CancellationToken cancellationToken = default)
    {
        var clusters = appModel.Resources.OfType<KindClusterResource>().ToList();
        if (clusters.Count == 0) return;

        _logger.LogInformation("Deleting {Count} Kind cluster(s)...", clusters.Count);

        // Best-effort parallel deletion — don't let one failure block others.
        await Task.WhenAll(clusters.Select(c => DeleteClusterAsync(c, cancellationToken)));
    }

    // ── Cluster operations ────────────────────────────────────────────────────

    private async Task CreateClusterAsync(KindClusterResource cluster, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating Kind cluster '{ClusterName}' → kubeconfig: {KubeconfigPath}",
            cluster.ClusterName, cluster.KubeconfigPath);

        var args = new List<string>
        {
            "create", "cluster",
            "--name", cluster.ClusterName,
            "--kubeconfig", cluster.KubeconfigPath,
        };

        string? tempConfigPath = null;
        try
        {
            var configPath = cluster.ConfigPath;

            // Auto-generate a Kind config when the user has set NodeCount/KubernetesVersion/PortMappings
            // but hasn't provided an explicit config file.
            if (configPath is null && NeedsGeneratedConfig(cluster))
            {
                tempConfigPath = Path.GetTempFileName();
                var configYaml = GenerateKindConfig(cluster);
                _logger.LogDebug("Auto-generated Kind config for '{ClusterName}':\n{Config}",
                    cluster.ClusterName, configYaml);
                await File.WriteAllTextAsync(tempConfigPath, configYaml, cancellationToken);
                configPath = tempConfigPath;
            }

            if (configPath is not null)
            {
                args.AddRange(["--config", configPath]);
            }

            var (exitCode, _, stderr) = await RunCommandAsync("kind", args, cancellationToken);

            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to create Kind cluster '{cluster.ClusterName}' (exit code {exitCode}). " +
                    $"Ensure the 'kind' CLI is installed and Docker is running.\n{stderr}");
            }

            _logger.LogInformation("Kind cluster '{ClusterName}' created successfully.", cluster.ClusterName);
        }
        finally
        {
            if (tempConfigPath is not null && File.Exists(tempConfigPath))
            {
                try { File.Delete(tempConfigPath); }
                catch { /* not critical */ }
            }
        }
    }

    private async Task WaitForReadyThenPostDeployAsync(
        KindClusterResource cluster, CancellationToken cancellationToken)
    {
        await WaitForClusterReadyAsync(cluster, cancellationToken);

        // Apply raw manifests first, then Helm charts (manifests often include CRDs required by charts).
        foreach (var manifest in cluster.ManifestPaths)
        {
            await ApplyManifestAsync(cluster, manifest, cancellationToken);
        }

        foreach (var chart in cluster.HelmCharts)
        {
            await InstallHelmChartAsync(cluster, chart, cancellationToken);
        }
    }

    private async Task WaitForClusterReadyAsync(KindClusterResource cluster, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Waiting for Kind cluster '{ClusterName}' to be ready (timeout: {Timeout})...",
            cluster.ClusterName, cluster.ReadyTimeout);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(cluster.ReadyTimeout);

        var delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= MaxHealthCheckAttempts; attempt++)
        {
            try
            {
                var (exitCode, _, _) = await RunCommandAsync(
                    "kubectl",
                    ["get", "nodes", "--kubeconfig", cluster.KubeconfigPath],
                    timeoutCts.Token);

                if (exitCode == 0)
                {
                    _logger.LogInformation("Kind cluster '{ClusterName}' is ready.", cluster.ClusterName);
                    return;
                }

                _logger.LogInformation(
                    "Kind cluster '{ClusterName}' not ready yet (attempt {Attempt}/{Max}), retrying in {Delay:F0}s...",
                    cluster.ClusterName, attempt, MaxHealthCheckAttempts, delay.TotalSeconds);
            }
            catch (OperationCanceledException) when (
                timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Kind cluster '{cluster.ClusterName}' did not become ready within {cluster.ReadyTimeout}.");
            }

            await Task.Delay(delay, timeoutCts.Token);

            // Exponential backoff capped at MaxRetryDelay.
            delay = TimeSpan.FromSeconds(
                Math.Min(delay.TotalSeconds * 2, MaxRetryDelay.TotalSeconds));
        }

        _logger.LogWarning(
            "Kind cluster '{ClusterName}' health check did not pass after {Max} attempts. " +
            "Continuing — the cluster may still be initialising.",
            cluster.ClusterName, MaxHealthCheckAttempts);
    }

    private async Task ApplyManifestAsync(
        KindClusterResource cluster, string manifestPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Applying manifest '{ManifestPath}' to cluster '{ClusterName}'.",
            manifestPath, cluster.ClusterName);

        var (exitCode, _, stderr) = await RunCommandAsync(
            "kubectl",
            ["apply", "-f", manifestPath, "--kubeconfig", cluster.KubeconfigPath],
            cancellationToken);

        if (exitCode != 0)
        {
            _logger.LogWarning(
                "Failed to apply manifest '{ManifestPath}' to cluster '{ClusterName}' (exit {ExitCode}): {Stderr}",
                manifestPath, cluster.ClusterName, exitCode, stderr);
        }
    }

    private async Task InstallHelmChartAsync(
        KindClusterResource cluster, KindHelmChart chart, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Installing Helm chart '{Chart}' as release '{ReleaseName}' in namespace '{Namespace}' on cluster '{ClusterName}'.",
            chart.Chart, chart.ReleaseName, chart.Namespace, cluster.ClusterName);

        var args = new List<string>
        {
            "install", chart.ReleaseName, chart.Chart,
            "--namespace", chart.Namespace,
            "--create-namespace",
            "--kubeconfig", cluster.KubeconfigPath,
        };

        if (chart.ValuesFile is not null)
        {
            args.AddRange(["-f", chart.ValuesFile]);
        }

        var (exitCode, _, stderr) = await RunCommandAsync("helm", args, cancellationToken);

        if (exitCode != 0)
        {
            _logger.LogWarning(
                "Failed to install Helm chart '{Chart}' (exit {ExitCode}): {Stderr}",
                chart.Chart, exitCode, stderr);
        }
    }

    private async Task DeleteClusterAsync(KindClusterResource cluster, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting Kind cluster '{ClusterName}'...", cluster.ClusterName);

        var (exitCode, _, _) = await RunCommandAsync(
            "kind",
            ["delete", "cluster", "--name", cluster.ClusterName],
            cancellationToken);

        if (exitCode != 0)
        {
            _logger.LogWarning(
                "Failed to delete Kind cluster '{ClusterName}'. " +
                "Run 'kind delete cluster --name {ClusterName}' manually to clean up.",
                cluster.ClusterName, cluster.ClusterName);
        }
        else
        {
            _logger.LogInformation("Kind cluster '{ClusterName}' deleted.", cluster.ClusterName);
        }

        // Best-effort kubeconfig cleanup.
        if (File.Exists(cluster.KubeconfigPath))
        {
            try
            {
                File.Delete(cluster.KubeconfigPath);
                _logger.LogDebug("Deleted kubeconfig '{KubeconfigPath}'.", cluster.KubeconfigPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not delete kubeconfig '{KubeconfigPath}'.", cluster.KubeconfigPath);
            }
        }
    }

    // ── Kind config generation ────────────────────────────────────────────────

    private static bool NeedsGeneratedConfig(KindClusterResource cluster) =>
        cluster.NodeCount > 0
        || cluster.KubernetesVersion is not null
        || cluster.PortMappings.Count > 0;

    /// <summary>
    /// Generates a Kind cluster YAML configuration from the resource's properties.
    /// Exposed as <c>internal</c> so tests can verify generation without running <c>kind</c>.
    /// </summary>
    internal static string GenerateKindConfig(KindClusterResource cluster)
    {
        var sb = new StringBuilder();
        sb.AppendLine("kind: Cluster");
        sb.AppendLine("apiVersion: kind.x-k8s.io/v1alpha4");
        sb.AppendLine("nodes:");

        // Control-plane node
        sb.AppendLine("  - role: control-plane");

        if (cluster.KubernetesVersion is not null)
        {
            sb.AppendLine($"    image: kindest/node:{cluster.KubernetesVersion}");
        }

        if (cluster.PortMappings.Count > 0)
        {
            sb.AppendLine("    extraPortMappings:");
            foreach (var pm in cluster.PortMappings)
            {
                sb.AppendLine($"    - containerPort: {pm.ContainerPort}");
                sb.AppendLine($"      hostPort: {pm.HostPort}");
                sb.AppendLine($"      protocol: {pm.Protocol}");
            }
        }

        // Worker nodes
        for (int i = 0; i < cluster.NodeCount; i++)
        {
            sb.AppendLine("  - role: worker");
            if (cluster.KubernetesVersion is not null)
            {
                sb.AppendLine($"    image: kindest/node:{cluster.KubernetesVersion}");
            }
        }

        return sb.ToString();
    }

    // ── Process runner ────────────────────────────────────────────────────────

    /// <summary>
    /// Runs an external process, capturing stdout and stderr without deadlocking on pipe buffers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses <see cref="ProcessStartInfo.ArgumentList"/> (discrete token array) instead of
    /// <see cref="ProcessStartInfo.Arguments"/> (raw string) to prevent command-injection attacks.
    /// </para>
    /// <para>
    /// Drains stdout and stderr concurrently with <see cref="Process.WaitForExitAsync"/> so that
    /// processes that produce large output (e.g., <c>kind create cluster</c>) do not deadlock on
    /// the OS pipe buffer.
    /// </para>
    /// </remarks>
    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCommandAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // Use ArgumentList (token array) — never interpolate into Arguments (string).
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Drain stdout and stderr concurrently with WaitForExitAsync.
        // Not doing so risks filling the OS pipe buffer and deadlocking the process.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Kill the process tree so no orphan child processes are left behind.
            try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }
}
