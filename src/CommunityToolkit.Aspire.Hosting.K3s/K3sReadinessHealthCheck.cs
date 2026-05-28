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
/// Then creates a short-lived <see cref="Kubernetes"/> client (disposed after each check)
/// to call <c>ListNodeAsync</c>, confirming that all expected nodes are <c>Ready</c>.
/// No <c>docker exec</c> is involved — works with any container runtime.
/// </para>
/// </summary>
internal sealed class K3sReadinessHealthCheck(
    K3sClusterResource resource,
    EndpointReference endpoint,
    Func<string, IKubernetes>? kubernetesFactory = null) : IHealthCheck
{
    private IKubernetes CreateClient(string path) =>
        kubernetesFactory is not null
            ? kubernetesFactory(path)
            : new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile(path));

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!endpoint.IsAllocated)
            return HealthCheckResult.Unhealthy("k3s API server endpoint not yet allocated");

        return await CheckCoreAsync(endpoint.Port, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Core readiness check given an already-known API server <paramref name="port"/>.
    /// Extracted so unit tests can exercise the full check path without requiring DCP
    /// to allocate the endpoint (i.e. without <see cref="EndpointReference.IsAllocated"/>
    /// being true).
    /// </summary>
    internal async Task<HealthCheckResult> CheckCoreAsync(int port, CancellationToken cancellationToken = default)
    {
        var dir = resource.KubeconfigDirectory;
        if (dir is null)
            return HealthCheckResult.Unhealthy("Kubeconfig directory not configured on resource");

        var rawPath = Path.Combine(dir, "cluster", "kubeconfig.yaml");
        if (!File.Exists(rawPath))
            return HealthCheckResult.Unhealthy("Waiting for k3s to write kubeconfig");

        try
        {
            var localPath = await EnsureKubeconfigVariantsAsync(rawPath, dir, port, cancellationToken)
                .ConfigureAwait(false);

            // Create a fresh client per check — no cached state, no stale connection risk.
            // At a 5-second health-check interval the TLS handshake overhead is negligible
            // for a local dev integration.
            using var k8sClient = CreateClient(localPath);

            var nodes = await k8sClient.CoreV1
                .ListNodeAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var readyCount = nodes.Items.Count(n =>
                n.Status?.Conditions?.Any(c =>
                    c.Type == "Ready" &&
                    string.Equals(c.Status, "True", StringComparison.OrdinalIgnoreCase)) == true);

            var expected = 1 + resource.AgentCount;
            if (readyCount < expected)
                return HealthCheckResult.Unhealthy($"k3s cluster: {readyCount}/{expected} nodes Ready");

            return HealthCheckResult.Healthy("k3s cluster is ready");
        }
        catch (Exception ex) when (IsTlsOrAuthFailure(ex))
        {
            // Stale kubeconfig — cluster was recreated with new certs (e.g. data volume wiped).
            // k3s has already written a fresh kubeconfig to rawPath (it writes once at startup,
            // not continuously), so we must NOT delete rawPath — that would remove the fresh
            // file and leave the health check waiting forever.
            // Delete only the derived variants so they are regenerated from the fresh raw file
            // on the next check cycle.
            TryDelete(Path.Combine(dir, "local", "kubeconfig.yaml"));
            TryDelete(Path.Combine(dir, "container", "kubeconfig.yaml"));
            return HealthCheckResult.Unhealthy("k3s kubeconfig is stale — retrying with fresh credentials");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }

    /// <summary>
    /// Reads the raw kubeconfig written by k3s, rewrites it for each consumer variant,
    /// and writes both to disk atomically. Returns the path to the local variant.
    /// </summary>
    private async Task<string> EnsureKubeconfigVariantsAsync(
        string rawPath,
        string dir,
        int port,
        CancellationToken ct)
    {
        var rawYaml = await File.ReadAllTextAsync(rawPath, ct).ConfigureAwait(false);
        var parsed = KubernetesYaml.Deserialize<K8SConfiguration>(rawYaml);

        var localDir = Path.Combine(dir, "local");
        Directory.CreateDirectory(localDir);
        var localPath = Path.Combine(localDir, "kubeconfig.yaml");
        await WriteAtomicAsync(localPath, BuildConfigYaml(parsed, $"https://localhost:{port}"), ct)
            .ConfigureAwait(false);

        var containerDir = Path.Combine(dir, "container");
        Directory.CreateDirectory(containerDir);
        await WriteAtomicAsync(
            Path.Combine(containerDir, "kubeconfig.yaml"),
            BuildConfigYaml(parsed, $"https://{resource.Name}:6443"),
            ct).ConfigureAwait(false);

        return localPath;
    }

    internal static string BuildConfigYaml(K8SConfiguration source, string serverUrl)
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

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/> atomically by first
    /// writing to a sibling temp file then renaming. Readers never observe a partial write.
    /// </summary>
    private static async Task WriteAtomicAsync(string path, string content, CancellationToken ct)
    {
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, content, ct).ConfigureAwait(false);
        File.Move(tmp, path, overwrite: true);
    }

    internal static bool IsTlsOrAuthFailure(Exception ex) =>
        ex is System.Security.Authentication.AuthenticationException
        || ex.InnerException is System.Security.Authentication.AuthenticationException
        || (ex is k8s.Autorest.HttpOperationException op &&
            op.Response?.StatusCode == System.Net.HttpStatusCode.Unauthorized);

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best effort */ }
    }
}
