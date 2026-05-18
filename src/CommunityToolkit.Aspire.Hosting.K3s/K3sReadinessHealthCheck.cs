using Aspire.Hosting.ApplicationModel;
using k8s;
using k8s.KubeConfigModels;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// Health check for <see cref="K3sClusterResource"/>.
/// <para>
/// Polls for the kubeconfig file written by k3s into the bind-mounted
/// <c>AppHostDirectory/.k3s/{name}/cluster/kubeconfig.yaml</c>. On first appearance
/// it rewrites the server URL for two variants:
/// <list type="bullet">
///   <item><c>local/kubeconfig.yaml</c> — <c>server: https://localhost:{allocatedPort}</c> (host processes)</item>
///   <item><c>container/kubeconfig.yaml</c> — <c>server: https://{name}:6443</c> (DCP-network containers)</item>
/// </list>
/// Then uses a cached <see cref="Kubernetes"/> client to call <c>ListNodeAsync</c>,
/// confirming that all expected nodes (server + agents) are in <c>Ready</c> state.
/// No <c>docker exec</c> is involved — works with any container runtime.
/// </para>
/// </summary>
internal sealed class K3sReadinessHealthCheck : IHealthCheck
{
    private readonly K3sClusterResource _resource;
    private readonly EndpointReference _endpoint;
    private Kubernetes? _cachedClient;

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
            return HealthCheckResult.Unhealthy("k3s API server endpoint not yet allocated");

        var dir = _resource.KubeconfigDirectory;
        if (dir is null)
            return HealthCheckResult.Unhealthy("Kubeconfig directory not configured on resource");

        var rawPath = Path.Combine(dir, "cluster", "kubeconfig.yaml");
        if (!File.Exists(rawPath))
            return HealthCheckResult.Unhealthy("Waiting for k3s to write kubeconfig");

        try
        {
            var client = await EnsureClientAsync(rawPath, cancellationToken).ConfigureAwait(false);

            var nodes = await client.CoreV1
                .ListNodeAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var readyCount = nodes.Items.Count(n =>
                n.Status?.Conditions?.Any(c =>
                    c.Type == "Ready" &&
                    string.Equals(c.Status, "True", StringComparison.OrdinalIgnoreCase)) == true);

            var expected = 1 + _resource.AgentCount;
            if (readyCount < expected)
                return HealthCheckResult.Unhealthy($"k3s cluster: {readyCount}/{expected} nodes Ready");

            return HealthCheckResult.Healthy("k3s cluster is ready");
        }
        catch (Exception ex) when (IsTlsOrAuthFailure(ex))
        {
            // Stale cached client — the cluster was recreated with new certs while the
            // health check held an old IKubernetes instance. k3s has already written a fresh
            // kubeconfig to rawPath (it writes once at startup, not continuously), so we must
            // NOT delete rawPath — that would remove the fresh file and leave the health check
            // waiting forever for a file that k3s will never rewrite.
            // Instead, discard only the cached client and the derived variants so they are
            // regenerated from the fresh raw file on the next check cycle.
            _cachedClient?.Dispose();
            _cachedClient = null;
            TryDelete(Path.Combine(dir, "local", "kubeconfig.yaml"));
            TryDelete(Path.Combine(dir, "container", "kubeconfig.yaml"));
            return HealthCheckResult.Unhealthy("k3s kubeconfig is stale — retrying with fresh credentials");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }

    private async Task<Kubernetes> EnsureClientAsync(string rawPath, CancellationToken ct)
    {
        if (_cachedClient is not null)
            return _cachedClient;

        var port = _endpoint.Port;
        var dir = _resource.KubeconfigDirectory!;

        var rawYaml = await File.ReadAllTextAsync(rawPath, ct).ConfigureAwait(false);
        var parsed = KubernetesYaml.Deserialize<K8SConfiguration>(rawYaml);

        var localDir = Path.Combine(dir, "local");
        Directory.CreateDirectory(localDir);
        var localPath = Path.Combine(localDir, "kubeconfig.yaml");
        await File.WriteAllTextAsync(localPath, BuildConfigYaml(parsed, $"https://localhost:{port}"), ct)
            .ConfigureAwait(false);

        var containerDir = Path.Combine(dir, "container");
        Directory.CreateDirectory(containerDir);
        await File.WriteAllTextAsync(
            Path.Combine(containerDir, "kubeconfig.yaml"),
            BuildConfigYaml(parsed, $"https://{_resource.Name}:6443"),
            ct).ConfigureAwait(false);

        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(localPath);
        _cachedClient = new Kubernetes(config);
        return _cachedClient;
    }

    private static string BuildConfigYaml(K8SConfiguration source, string serverUrl)
    {
        var yaml = KubernetesYaml.Serialize(source);
        var copy = KubernetesYaml.Deserialize<K8SConfiguration>(yaml);
        foreach (var cluster in copy.Clusters ?? [])
        {
            if (cluster.ClusterEndpoint is not null)
                cluster.ClusterEndpoint.Server = serverUrl;
        }

        return KubernetesYaml.Serialize(copy);
    }

    private static bool IsTlsOrAuthFailure(Exception ex) =>
        ex is System.Security.Authentication.AuthenticationException
        || ex.InnerException is System.Security.Authentication.AuthenticationException
        || (ex is k8s.Autorest.HttpOperationException op &&
            op.Response?.StatusCode == System.Net.HttpStatusCode.Unauthorized);

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best effort */ }
    }
}
