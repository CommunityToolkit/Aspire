using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using k8s;
using k8s.KubeConfigModels;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// Health check for <see cref="K3sClusterResource"/>.
/// <para>
/// Instead of probing <c>GET /healthz</c> (which requires authentication in Kubernetes 1.28+
/// because anonymous-auth defaults to <c>false</c>), this check runs
/// <c>docker exec {container} kubectl get nodes</c> inside the k3s container.
/// kubectl inside the container uses the default in-cluster kubeconfig, so no external
/// credentials are needed and the result is authoritative: a node in <c>Ready</c> state
/// proves the API server, scheduler, and kubelet are all functional.
/// </para>
/// <para>
/// On first success the kubeconfig is read from the container via <c>docker exec cat</c>,
/// parsed into two <see cref="K8SConfiguration"/> variants, and stored on the resource:
/// <list type="bullet">
///   <item><see cref="K3sClusterResource.AdminKubeconfig"/> — <c>server: https://localhost:{port}</c></item>
///   <item><see cref="K3sClusterResource.ContainerKubeconfig"/> — <c>server: https://{name}:6443</c></item>
/// </list>
/// </para>
/// </summary>
internal sealed class K3sReadinessHealthCheck : IHealthCheck
{
    private readonly K3sClusterResource _resource;
    private readonly EndpointReference _endpoint;
    private bool _kubeconfigRead;

    internal K3sReadinessHealthCheck(K3sClusterResource resource, EndpointReference endpoint)
    {
        _resource = resource;
        _endpoint = endpoint;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_endpoint.IsAllocated)
        {
            return HealthCheckResult.Unhealthy("k3s API server endpoint not yet allocated");
        }

        var port = _endpoint.Port;

        try
        {
            var containerId = await FindContainerIdAsync(cancellationToken);

            if (containerId is null)
            {
                return HealthCheckResult.Unhealthy("k3s container not yet found via docker ps");
            }

            // Run kubectl get nodes inside the container where the default kubeconfig is
            // already configured — avoids any authentication issue from the outside.
            var nodesOutput = await RunDockerAsync(
                ["exec", containerId,
                 "kubectl", "get", "nodes",
                 "--kubeconfig", "/etc/rancher/k3s/k3s.yaml",
                 "--no-headers"],
                cancellationToken);

            if (nodesOutput is null)
            {
                return HealthCheckResult.Unhealthy(
                    "kubectl get nodes failed — k3s API server not yet ready");
            }

            // Count nodes actually in Ready state (excluding NotReady ones).
            var readyNodeLines = nodesOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Contains("Ready") && !line.Contains("NotReady"))
                .ToArray();

            // For multi-node clusters (WithAgentNodes), wait for server + all agents.
            // For single-node, 1 Ready node is sufficient.
            var expectedNodes = 1 + _resource.AgentCount;

            if (readyNodeLines.Length < expectedNodes)
            {
                return HealthCheckResult.Unhealthy(
                    $"k3s cluster: {readyNodeLines.Length}/{expectedNodes} nodes Ready");
            }

            if (!_kubeconfigRead)
            {
                var rawYaml = await RunDockerAsync(
                    ["exec", containerId, "cat", "/etc/rancher/k3s/k3s.yaml"],
                    cancellationToken);

                if (rawYaml is null)
                {
                    return HealthCheckResult.Unhealthy(
                        "k3s kubeconfig not yet available inside the container");
                }

                var parsed = KubernetesYaml.Deserialize<K8SConfiguration>(rawYaml);

                _resource.AdminKubeconfig =
                    BuildConfig(parsed, $"https://localhost:{port}");
                _resource.ContainerKubeconfig =
                    BuildConfig(parsed, $"https://{_resource.Name}:6443");
                _kubeconfigRead = true;
            }

            return HealthCheckResult.Healthy("k3s cluster is ready");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }

    private static K8SConfiguration BuildConfig(K8SConfiguration source, string serverUrl)
    {
        var yaml = KubernetesYaml.Serialize(source);
        var copy = KubernetesYaml.Deserialize<K8SConfiguration>(yaml);

        foreach (var cluster in copy.Clusters ?? [])
        {
            if (cluster.ClusterEndpoint is not null)
            {
                cluster.ClusterEndpoint.Server = serverUrl;
            }
        }

        return copy;
    }

    private async Task<string?> FindContainerIdAsync(CancellationToken ct)
    {
        // docker ps --filter name=VALUE uses substring matching: "name=k8s" also matches
        // "k8s-agent-0", "k8s-agent-1", etc. Use --format to get names alongside IDs and
        // exclude agent containers whose names contain "-agent-".
        var output = await RunDockerAsync(
            ["ps",
             "--filter", $"name={_resource.Name}",
             "--format", "{{.ID}}\t{{.Names}}",
             "--no-trunc"],
            ct);

        if (output is null)
        {
            return null;
        }

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t', 2))
            .Where(parts => parts.Length == 2 && !parts[1].Contains("-agent-"))
            .Select(parts => parts[0].Trim())
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
    }

    private static async Task<string?> RunDockerAsync(string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            return null;
        }

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
            ? output
            : null;
    }
}
