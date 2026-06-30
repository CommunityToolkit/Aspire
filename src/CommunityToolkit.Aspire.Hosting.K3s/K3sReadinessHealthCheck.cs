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
        // Resolve the API server port. We try two sources in order:
        //
        // 1. EndpointAnnotation.AllocatedEndpoint.Port — set by DCP when it allocates the
        //    endpoint. Non-blocking (returns null if not yet set) and NOT cached, so it picks
        //    up DCP's allocation on any tick after the event fires.
        //    We intentionally avoid EndpointReference.IsAllocated here because that property
        //    caches its result on first call; if the health check ticks before DCP fires the
        //    allocation event the cached false would persist forever.
        //
        // 2. EndpointAnnotation.Port — the statically configured host port (only non-null
        //    when the caller passed an explicit apiServerPort to AddK3sCluster). Works for
        //    both C# and polyglot AppHosts.

        var annotation = resource.Annotations
            .OfType<EndpointAnnotation>()
            .FirstOrDefault(a => a.Name == K3sClusterResource.ApiServerEndpointName);

        int port;
        if (annotation?.AllocatedEndpoint is { Port: > 0 } alloc)
        {
            port = alloc.Port;
        }
        else if (annotation?.Port is > 0)
        {
            port = annotation.Port!.Value;
        }
        else
        {
            return HealthCheckResult.Unhealthy("k3s API server endpoint not yet allocated");
        }

        return await CheckCoreAsync(port, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Core readiness check given an already-known API server <paramref name="port"/>.
    /// Extracted so unit tests can call it directly with an explicit port.
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
    /// and writes both to disk in-place. Returns the path to the local variant.
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
        await WriteKubeconfigAsync(localPath, BuildConfigYaml(parsed, $"https://localhost:{port}"), ct)
            .ConfigureAwait(false);

        var containerDir = Path.Combine(dir, "container");
        Directory.CreateDirectory(containerDir);
        await WriteKubeconfigAsync(
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
    /// Writes <paramref name="content"/> to <paramref name="path"/> by truncating the file
    /// in-place, preserving its inode.
    /// </summary>
    /// <remarks>
    /// A rename-based atomic write (write temp → rename over target) creates a new inode at
    /// the destination path. On Linux, Docker file-level bind mounts track the inode that
    /// existed at container-start time, not the path. Containers with a file-level bind mount
    /// of this kubeconfig path (helm, kubectl) would never see the updated content if the
    /// file were replaced by rename. Writing in-place (O_TRUNC on the existing file) keeps
    /// the original inode and makes the update immediately visible inside those containers.
    /// On macOS, Docker Desktop resolves bind-mounted files by path (virtiofs/gRPC-FUSE), so
    /// both approaches work there — the in-place write is the cross-platform correct choice.
    /// </remarks>
    private static Task WriteKubeconfigAsync(string path, string content, CancellationToken ct) =>
        File.WriteAllTextAsync(path, content, ct);

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
