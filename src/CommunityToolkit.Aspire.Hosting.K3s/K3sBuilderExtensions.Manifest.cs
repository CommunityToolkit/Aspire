using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting;
using k8s;
using k8s.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for applying Kubernetes YAML manifests to a k3s cluster.
/// </summary>
public static class K3sManifestBuilderExtensions
{
    /// <summary>
    /// Applies one or more Kubernetes YAML files to the cluster via
    /// <c>kubectl apply --server-side</c> (Server-Side Apply). No bind-mount is required.
    /// <list type="bullet">
    ///   <item>A single file: <c>cluster.AddK8sManifest("crd", "./k8s/widget-crd.yaml")</c></item>
    ///   <item>A directory: all <c>.yaml</c>/<c>.yml</c> files applied lexicographically.</item>
    ///   <item>A glob: <c>"./k8s/crds/*.yaml"</c></item>
    /// </list>
    /// CRDs are detected automatically; the resource waits for the <c>Established</c>
    /// condition via the KubernetesClient before transitioning to <c>Running</c>, so
    /// <c>WaitFor(manifest)</c> correctly gates dependent operators.
    /// </summary>
    [AspireExport("addK8sManifest", Description = "Applies Kubernetes YAML manifests to the k3s cluster")]
    public static IResourceBuilder<K8sManifestResource> AddK8sManifest(
        this IResourceBuilder<K3sClusterResource> builder,
        [ResourceName] string name,
        string path)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(path);

        var cluster = builder.Resource;
        var manifest = new K8sManifestResource(name, path, cluster);

        cluster.AddManifest(manifest.Name);

        var healthCheckKey = $"manifest_{name}_ready";
        builder.ApplicationBuilder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            healthCheckKey,
            sp => new K8sManifestHealthCheck(manifest),
            failureStatus: HealthStatus.Unhealthy,
            tags: null));

        return builder.ApplicationBuilder
            .AddResource(manifest)
            .ExcludeFromManifest()
            .WithHealthCheck(healthCheckKey)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "K8s Manifest",
                State = KnownResourceStates.NotStarted,
                Properties = [new ResourcePropertySnapshot("Path", path)],
            });
    }

    /// <summary>
    /// Exposes a Kubernetes service from this manifest as a clickable endpoint in the dashboard.
    /// Traffic is forwarded in-process via the KubernetesClient WebSocket API.
    /// </summary>
    /// <param name="builder">The manifest resource builder.</param>
    /// <param name="serviceName">The Kubernetes service name.</param>
    /// <param name="servicePort">The service port number.</param>
    /// <param name="name">Friendly name shown in the dashboard.</param>
    /// <param name="namespace">
    /// The namespace containing the service. Defaults to <c>"default"</c>.
    /// For remote manifests (HTTP URLs) the namespace must be specified explicitly.
    /// </param>
    public static IResourceBuilder<K8sManifestResource> WithEndpoint(
        this IResourceBuilder<K8sManifestResource> builder,
        string serviceName,
        int servicePort,
        string name,
        string @namespace = "default")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceName);
        ArgumentNullException.ThrowIfNull(name);

        builder.Resource.EndpointDefinitions.Add(
            new ManifestEndpointDefinition(serviceName, servicePort, name, @namespace));

        return builder;
    }

    // ── Lifecycle (called from the cluster's ResourceReadyEvent) ──────────────

    internal static async Task RunManifestAsync(
        K8sManifestResource manifest,
        K3sClusterResource cluster,
        ResourceNotificationService notifications,
        ILogger logger,
        CancellationToken ct)
    {
        await notifications.PublishUpdateAsync(manifest,
            state => state with { State = KnownResourceStates.Starting })
            .ConfigureAwait(false);

        try
        {
            var kubeconfigYaml = K3sBuilderExtensions.GetAdminKubeconfigYaml(cluster);

            if (kubeconfigYaml is null)
            {
                throw new InvalidOperationException(
                    "k3s kubeconfig is not yet available when applying manifest.");
            }

            // Write a temp kubeconfig file — kubectl requires a path, same as helm.
            var tempKubeconfig = Path.Combine(
                Path.GetTempPath(),
                $"aspire-k3s-manifest-{Environment.ProcessId}-{manifest.Name}.yaml");

            await File.WriteAllTextAsync(tempKubeconfig, kubeconfigYaml, Encoding.UTF8, ct)
                .ConfigureAwait(false);

            try
            {
                var files = ResolveFiles(manifest.Path);

                logger.LogInformation(
                    "Applying {Count} manifest file(s) from '{Path}'", files.Count, manifest.Path);

                // kubectl apply --server-side (SSA) for each file.
                foreach (var file in files)
                {
                    await KubectlApplyAsync(file, tempKubeconfig, logger, ct)
                        .ConfigureAwait(false);
                }

                // Wait for any CRDs to reach Established using the KubernetesClient.
                await WaitForCrdsEstablishedAsync(
                    files, kubeconfigYaml, logger, ct).ConfigureAwait(false);
            }
            finally
            {
                try { File.Delete(tempKubeconfig); } catch { /* best effort */ }
            }

            manifest.IsReady = true;

            // Start in-process port-forwards for any declared endpoints.
            var urls = ImmutableArray<UrlSnapshot>.Empty;
            if (manifest.EndpointDefinitions.Count > 0)
            {
                urls = await StartPortForwardAsync(manifest, kubeconfigYaml, logger, ct)
                    .ConfigureAwait(false);
            }

            await notifications.PublishUpdateAsync(manifest, state => state with
            {
                State = KnownResourceStates.Running,
                Urls = urls,
                Properties =
                [
                    .. state.Properties.Where(p => p.Name is not "Path"),
                    new ResourcePropertySnapshot("Path", manifest.Path),
                ],
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to apply manifest '{Name}'.", manifest.Name);

            await notifications.PublishUpdateAsync(manifest,
                state => state with { State = KnownResourceStates.FailedToStart })
                .ConfigureAwait(false);
        }
    }

    // ── kubectl apply ─────────────────────────────────────────────────────────

    private static async Task KubectlApplyAsync(
        string file,
        string kubeconfigPath,
        ILogger logger,
        CancellationToken ct)
    {
        logger.LogInformation("Applying {File}", file);

        var psi = new ProcessStartInfo("kubectl")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("apply");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(file);
        psi.ArgumentList.Add($"--kubeconfig={kubeconfigPath}");
        psi.ArgumentList.Add("--server-side");
        psi.ArgumentList.Add("--field-manager=aspire-k3s");
        psi.ArgumentList.Add("--force-conflicts");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start kubectl process.");

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) logger.LogInformation("{Line}", e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) logger.LogWarning("{Line}", e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"kubectl apply failed for '{file}' (exit code {process.ExitCode}).");
        }
    }

    // ── Port-forward for manifest endpoints ──────────────────────────────────

    private static async Task<ImmutableArray<UrlSnapshot>> StartPortForwardAsync(
        K8sManifestResource manifest,
        string kubeconfigYaml,
        ILogger logger,
        CancellationToken ct)
    {
        var urls = ImmutableArray.CreateBuilder<UrlSnapshot>();

        foreach (var ep in manifest.EndpointDefinitions)
        {
            var hostPort = AllocateHostPort();

            var forwarder = new K3sInProcessPortForwarder(
                kubeconfigYaml,
                ep.Namespace,
                ep.ServiceName,
                hostPort,
                ep.ServicePort);

            _ = forwarder.RunAsync(logger, ct);

            var scheme = ep.ServicePort is 443 or 8443 ? "https" : "http";
            urls.Add(new UrlSnapshot(ep.EndpointName, $"{scheme}://localhost:{hostPort}", IsInternal: false));
        }

        return urls.ToImmutable();
    }

    private static int AllocateHostPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // ── CRD readiness (in-process via KubernetesClient) ──────────────────────

    private static async Task WaitForCrdsEstablishedAsync(
        IReadOnlyList<string> files,
        string kubeconfigYaml,
        ILogger logger,
        CancellationToken ct)
    {
        var crdNames = new List<string>();

        foreach (var file in files)
        {
            // Skip remote URLs — kubectl downloaded and applied them, but we can't parse
            // them with KubernetesYaml locally. CRD detection for remote files is skipped;
            // if you need CRD readiness gating, use a local file path instead.
            if (file.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                file.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var objects = await KubernetesYaml.LoadAllFromFileAsync(file)
                .ConfigureAwait(false);

            foreach (var obj in objects)
            {
                if (obj is V1CustomResourceDefinition crd && crd.Metadata?.Name is { } crdName)
                {
                    crdNames.Add(crdName);
                }
            }
        }

        if (crdNames.Count == 0)
        {
            return;
        }

        using var configStream = new MemoryStream(Encoding.UTF8.GetBytes(kubeconfigYaml));
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(configStream);
        using var k8sClient = new Kubernetes(config);

        foreach (var crdName in crdNames)
        {
            await WaitForCrdEstablishedAsync(k8sClient, crdName, logger, ct)
                .ConfigureAwait(false);
        }
    }

    private static async Task WaitForCrdEstablishedAsync(
        Kubernetes k8sClient,
        string crdName,
        ILogger logger,
        CancellationToken ct)
    {
        logger.LogInformation("Waiting for CRD '{Crd}' to reach Established...", crdName);

        while (!ct.IsCancellationRequested)
        {
            var crd = await k8sClient.ApiextensionsV1
                .ReadCustomResourceDefinitionAsync(crdName, cancellationToken: ct)
                .ConfigureAwait(false);

            var established = crd.Status?.Conditions?.Any(c =>
                c.Type == "Established" &&
                string.Equals(c.Status, "True", StringComparison.OrdinalIgnoreCase)) == true;

            if (established)
            {
                logger.LogInformation("CRD '{Crd}' is Established.", crdName);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
    }

    // ── File resolution ───────────────────────────────────────────────────────

    // Exposed for unit tests via InternalsVisibleTo.
    internal static IReadOnlyList<string> ResolveFilesForTest(string path) =>
        ResolveFiles(path);

    private static IReadOnlyList<string> ResolveFiles(string path)
    {
        // kubectl apply -f supports HTTPS URLs natively — pass through as-is.
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return [path];
        }

        if (Directory.Exists(path))
        {
            return [
                ..Directory.GetFiles(path, "*.yaml", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles(path, "*.yml", SearchOption.TopDirectoryOnly))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
            ];
        }

        var dir = System.IO.Path.GetDirectoryName(path) ?? ".";
        var pattern = System.IO.Path.GetFileName(path);

        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            return [..Directory.GetFiles(dir, pattern).Order(StringComparer.OrdinalIgnoreCase)];
        }

        return [path];
    }
}

/// <summary>
/// Health check that satisfies <c>WaitFor(manifest)</c>.
/// Returns <see cref="HealthCheckResult.Healthy"/> once all files are applied
/// and any CRDs have reached the <c>Established</c> condition.
/// </summary>
internal sealed class K8sManifestHealthCheck(K8sManifestResource manifest) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(manifest.IsReady
            ? HealthCheckResult.Healthy("Manifests applied")
            : HealthCheckResult.Unhealthy("Manifests not yet applied"));
}

#pragma warning restore ASPIREATS001
