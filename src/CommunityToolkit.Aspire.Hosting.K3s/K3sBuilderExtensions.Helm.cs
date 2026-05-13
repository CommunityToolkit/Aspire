using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Helm release resources to a k3s cluster.
/// </summary>
public static class K3sHelmBuilderExtensions
{
    /// <summary>
    /// Adds a Helm release as a child resource of the k3s cluster.
    /// <para>
    /// Follows the same pattern as <c>PostgresServerResource.AddDatabase</c>: the release is
    /// registered on the parent cluster, and the cluster's own <see cref="ResourceReadyEvent"/>
    /// handler drives the install lifecycle for all registered releases. Helm output streams to
    /// each release's individual log tab in the dashboard.
    /// </para>
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="name">Resource name — also the Helm release name.</param>
    /// <param name="chart">Chart name. Add <paramref name="repo"/> for remote charts.</param>
    /// <param name="repo">Optional Helm repository URL (passed as <c>--repo</c>).</param>
    /// <param name="version">Optional chart version (<c>--version</c>).</param>
    /// <param name="namespace">Target namespace (created automatically).</param>
    /// <returns>A builder for the <see cref="HelmReleaseResource"/>.</returns>
    [AspireExport("addHelmRelease", Description = "Adds a Helm chart release to the k3s cluster")]
    public static IResourceBuilder<HelmReleaseResource> AddHelmRelease(
        this IResourceBuilder<K3sClusterResource> builder,
        [ResourceName] string name,
        string chart,
        string? repo = null,
        string? version = null,
        string @namespace = "default")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(chart);

        var cluster = builder.Resource;

        var release = new HelmReleaseResource(name, releaseName: name, @namespace, cluster)
        {
            Chart = chart,
            RepoUrl = repo,
            Version = version,
        };

        // Register the release on the parent cluster — mirrors PostgresServerResource.AddDatabase().
        cluster.AddHelmRelease(release.Name, release.ReleaseName);

        // Health check that satisfies WaitFor(helmRelease) on dependent resources.
        // Returns Healthy only after the install lifecycle sets release.IsReady = true.
        var healthCheckKey = $"helm_{name}_ready";
        builder.ApplicationBuilder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            healthCheckKey,
            sp => new HelmReleaseHealthCheck(release),
            failureStatus: HealthStatus.Unhealthy,
            tags: null));

        return builder.ApplicationBuilder
            .AddResource(release)
            .ExcludeFromManifest()
            .WithHealthCheck(healthCheckKey)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Helm Release",
                State = KnownResourceStates.NotStarted,
                Properties =
                [
                    new ResourcePropertySnapshot("ReleaseName", name),
                    new ResourcePropertySnapshot("Chart", chart),
                    new ResourcePropertySnapshot("Namespace", @namespace),
                    new ResourcePropertySnapshot("Version", version ?? "latest"),
                ],
            });
    }

    /// <summary>
    /// Adds a Helm <c>--set key=value</c> argument to this release.
    /// </summary>
    public static IResourceBuilder<HelmReleaseResource> WithHelmValue(
        this IResourceBuilder<HelmReleaseResource> builder,
        string key,
        string value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        builder.Resource.HelmValues[key] = value;
        return builder;
    }

    /// <summary>
    /// Exposes a Kubernetes service from this release as a clickable endpoint in the dashboard.
    /// The NodePort is auto-discovered and forwarded in-process via the KubernetesClient WebSocket API.
    /// </summary>
    public static IResourceBuilder<HelmReleaseResource> WithEndpoint(
        this IResourceBuilder<HelmReleaseResource> builder,
        string serviceName,
        int servicePort,
        string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceName);
        ArgumentNullException.ThrowIfNull(name);

        builder.Resource.EndpointDefinitions.Add(
            new HelmEndpointDefinition(serviceName, servicePort, name));

        return builder;
    }

    // ── Lifecycle (driven from the parent cluster's ResourceReadyEvent) ────────

    /// <summary>
    /// Runs the full install lifecycle for <paramref name="release"/>:
    /// helm install → NodePort discovery → in-process port-forward → Running.
    /// Called by the parent cluster's <see cref="ResourceReadyEvent"/> handler —
    /// the same pattern Postgres uses for database creation in <c>AddPostgres</c>.
    /// </summary>
    internal static async Task RunReleaseAsync(
        HelmReleaseResource release,
        K3sClusterResource cluster,
        ResourceNotificationService notifications,
        ILogger logger,
        CancellationToken ct)
    {
        await notifications.PublishUpdateAsync(release,
            state => state with { State = KnownResourceStates.Starting })
            .ConfigureAwait(false);

        try
        {
            var kubeconfigYaml = K3sBuilderExtensions.GetAdminKubeconfigYaml(cluster);

            if (kubeconfigYaml is null)
            {
                throw new InvalidOperationException(
                    "k3s kubeconfig is not yet available. " +
                    "The cluster ResourceReadyEvent fired before the health check populated the kubeconfig.");
            }

            await RunHelmAsync(release, kubeconfigYaml, logger, ct).ConfigureAwait(false);

            var urls = release.EndpointDefinitions.Count > 0
                ? await DiscoverAndStartPortForwardAsync(release, kubeconfigYaml, logger, ct)
                    .ConfigureAwait(false)
                : ImmutableArray<UrlSnapshot>.Empty;

            // Set before PublishUpdateAsync so the health check unblocks WaitFor callers
            // as soon as the Running state notification is processed.
            release.IsReady = true;

            await notifications.PublishUpdateAsync(release, state => state with
            {
                State = KnownResourceStates.Running,
                Urls = urls,
                // Merge: keep all existing properties (the orchestrator injects ParentName which
                // drives parent-child display in the dashboard) and only update/add our own.
                // Replacing the entire Properties array would wipe out ParentName, causing the
                // release to lose its parent and appear at the top level after going Running.
                Properties =
                [
                    .. state.Properties.Where(p =>
                        p.Name is not ("ReleaseName" or "Chart" or "Namespace" or "Version" or "ChartVersion")),
                    new ResourcePropertySnapshot("ReleaseName", release.ReleaseName),
                    new ResourcePropertySnapshot("Chart", release.Chart!),
                    new ResourcePropertySnapshot("ChartVersion", release.Version ?? "latest"),
                    new ResourcePropertySnapshot("Namespace", release.Namespace),
                ],
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Helm release '{Name}' failed.", release.ReleaseName);

            await notifications.PublishUpdateAsync(release,
                state => state with { State = KnownResourceStates.FailedToStart })
                .ConfigureAwait(false);
        }
    }

    // ── helm subprocess ───────────────────────────────────────────────────────

    private static async Task RunHelmAsync(
        HelmReleaseResource release,
        string kubeconfigYaml,
        ILogger logger,
        CancellationToken ct)
    {
        var tempKubeconfig = Path.Combine(
            Path.GetTempPath(),
            $"aspire-k3s-helm-{Environment.ProcessId}-{release.ReleaseName}.yaml");

        await File.WriteAllTextAsync(tempKubeconfig, kubeconfigYaml, Encoding.UTF8, ct)
            .ConfigureAwait(false);

        try
        {
            // When a repo URL is provided, add/update the repo first.
            // The --repo shorthand in helm upgrade --install does not work reliably for
            // all chart repositories (e.g. kubernetes-dashboard returns 404 via --repo).
            // Official docs always show: helm repo add → helm repo update → helm install.
            string? repoAlias = null;
            if (release.RepoUrl is not null)
            {
                repoAlias = $"aspire-k3s-{release.ReleaseName}";
                await RunHelmCommandAsync(
                    logger,
                    ["repo", "add", "--force-update", repoAlias, release.RepoUrl],
                    ct).ConfigureAwait(false);

                await RunHelmCommandAsync(
                    logger,
                    ["repo", "update", repoAlias],
                    ct).ConfigureAwait(false);
            }

            var args = BuildHelmInstallArgs(
                release.ReleaseName, release.Chart!, repoAlias,
                release.Version, release.Namespace, release.HelmValues,
                tempKubeconfig);

            logger.LogInformation("Running: helm {Args}", string.Join(' ', args));

            await RunHelmCommandAsync(logger, args, ct).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(tempKubeconfig); } catch { /* best effort */ }
        }
    }

    // ── Argument builder ──────────────────────────────────────────────────────

    /// <summary>Runs a <c>helm</c> subcommand, logging output and failing on non-zero exit.</summary>
    private static async Task RunHelmCommandAsync(
        ILogger logger,
        IEnumerable<string> args,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo("helm")
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

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start helm process.");

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
                $"helm {string.Join(' ', psi.ArgumentList)} failed (exit code {process.ExitCode}).");
        }
    }

    internal static IReadOnlyList<string> BuildHelmInstallArgs(
        string releaseName,
        string chart,
        string? repoAlias,   // null = no repo, non-null = "{alias}/{chart}" notation
        string? version,
        string @namespace,
        IReadOnlyDictionary<string, string>? values,
        string kubeconfigPath)
    {
        // When a repo alias was registered via `helm repo add`, use "alias/chart" notation.
        // Otherwise treat the chart as a path or OCI reference.
        var chartRef = repoAlias is not null ? $"{repoAlias}/{chart}" : chart;

        var args = new List<string>
        {
            "upgrade", "--install",
            releaseName,
            chartRef,
            "--namespace", @namespace,
            "--create-namespace",
            "--wait",
            "--timeout", "10m",
            $"--kubeconfig={kubeconfigPath}",
        };

        if (version is not null)
        {
            args.Add("--version");
            args.Add(version);
        }

        if (values is not null)
        {
            foreach (var (key, value) in values)
            {
                args.Add("--set");
                args.Add($"{key}={value}");
            }
        }

        return args;
    }

    // ── Port-forward ──────────────────────────────────────────────────────────

    private static async Task<ImmutableArray<UrlSnapshot>> DiscoverAndStartPortForwardAsync(
        HelmReleaseResource release,
        string kubeconfigYaml,
        ILogger logger,
        CancellationToken ct)
    {
        var urls = ImmutableArray.CreateBuilder<UrlSnapshot>();

        using var configStream = new MemoryStream(Encoding.UTF8.GetBytes(kubeconfigYaml));
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(configStream);
        using var k8sClient = new Kubernetes(config);

        foreach (var ep in release.EndpointDefinitions)
        {
            var nodePort = await DiscoverNodePortAsync(
                k8sClient, release.ReleaseName, release.Namespace,
                ep.ServiceName, ep.ServicePort, logger, ct).ConfigureAwait(false);

            var hostPort = AllocateHostPort();

            var forwarder = new K3sInProcessPortForwarder(
                kubeconfigYaml,
                release.Namespace,
                ep.ServiceName,   // look up by service name, not release label
                hostPort,
                ep.ServicePort);

            _ = forwarder.RunAsync(logger, ct);

            var scheme = ep.ServicePort is 443 or 8443 ? "https" : "http";
            urls.Add(new UrlSnapshot(ep.EndpointName, $"{scheme}://localhost:{hostPort}", IsInternal: false));

            if (nodePort.HasValue)
            {
                urls.Add(new UrlSnapshot(
                    $"{ep.EndpointName} (container)",
                    $"{scheme}://{release.Parent.Name}:{nodePort.Value}",
                    IsInternal: true));
            }
        }

        return urls.ToImmutable();
    }

    private static async Task<int?> DiscoverNodePortAsync(
        Kubernetes k8sClient,
        string releaseName,
        string @namespace,
        string serviceName,
        int servicePort,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var services = await k8sClient.CoreV1.ListNamespacedServiceAsync(
                @namespace,
                labelSelector: $"app.kubernetes.io/instance={releaseName}",
                cancellationToken: ct).ConfigureAwait(false);

            var port = services.Items
                .FirstOrDefault(s => string.Equals(
                    s.Metadata.Name, serviceName, StringComparison.OrdinalIgnoreCase))
                ?.Spec.Ports
                .FirstOrDefault(p => p.Port == servicePort);

            if (port?.NodePort is null)
            {
                logger.LogWarning(
                    "NodePort for {ServiceName}:{ServicePort} not found; container URL omitted.",
                    serviceName, servicePort);
            }

            return port?.NodePort;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NodePort discovery failed for service '{ServiceName}'.", serviceName);
            return null;
        }
    }

    private static int AllocateHostPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

#pragma warning restore ASPIREATS001
