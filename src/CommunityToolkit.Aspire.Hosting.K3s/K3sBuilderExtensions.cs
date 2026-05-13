using System.Text;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;
using System.Net.Sockets;

#pragma warning disable ASPIREATS001 // AspireExport is experimental
#pragma warning disable ASPIRECERTIFICATES001 // WithHttpsDeveloperCertificate is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding k3s cluster resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class K3sBuilderExtensions
{
    /// <summary>
    /// Adds a k3s Kubernetes cluster resource to the distributed application.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name used for DNS resolution within the DCP network.</param>
    /// <param name="apiServerPort">
    /// Optional host port to bind the Kubernetes API server (port 6443) to.
    /// When <see langword="null"/> a random available port is assigned.
    /// </param>
    /// <param name="configure">Optional callback to configure <see cref="K3sClusterOptions"/>.</param>
    /// <returns>A builder for the <see cref="K3sClusterResource"/>.</returns>
    [AspireExport("addK3sCluster", Description = "Adds a k3s Kubernetes cluster resource")]
    public static IResourceBuilder<K3sClusterResource> AddK3sCluster(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? apiServerPort = null,
        Action<K3sClusterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var options = new K3sClusterOptions();
        configure?.Invoke(options);

        var resource = new K3sClusterResource(name);
        var tag = options.ImageTag ?? K3sContainerImageTags.Tag;

        var resourceBuilder = builder.AddResource(resource)
            .WithImage(K3sContainerImageTags.Image, tag)
            .WithImageRegistry(K3sContainerImageTags.Registry)

            // ── k3d-style init entrypoint ─────────────────────────────────────
            // Runs mount --make-rshared / and the cgroupsv2 evacuation fix before
            // k3s starts — exactly what k3d does via /bin/k3d-entrypoint*.sh.
            // WithContainerFiles injects the script via `docker cp` — no bind mounts,
            // no host-side temp files, works on all platforms.
            .WithContainerFiles("/", [new ContainerFile
            {
                Name = "aspire-k3s-entrypoint.sh",
                Contents = K3sInitEntrypointScript,
                Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                     | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                     | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
            }])
            .WithEntrypoint("/bin/sh")
            .WithArgs("/aspire-k3s-entrypoint.sh")

            // ── k3s server command ────────────────────────────────────────────
            .WithArgs("server")

            // NOTE: --cluster-init (embedded etcd) is intentionally NOT used.
            // etcd stores the container's IP in its peer URL. When Docker assigns a new IP
            // on container restart, etcd refuses to start ("not a member of the etcd cluster").
            // k3s uses SQLite by default for single-server clusters — it handles restarts and
            // IP changes gracefully. k3d only adds --cluster-init for HA multi-server setups.

            // Add TLS SANs so the API server certificate is valid for all addresses
            // that clients use to reach it: host-side (localhost), DCP-network containers
            // ({resourceName}), and any forwarded address (0.0.0.0).
            .WithArgs($"--tls-san=127.0.0.1")
            .WithArgs($"--tls-san=localhost")
            .WithArgs($"--tls-san={name}")
            .WithArgs("--tls-san=0.0.0.0")

            // Disable components not needed for local development.
            // servicelb and metrics-server are the biggest resource consumers and slowest
            // to start; disabling them speeds up cluster readiness significantly.
            .WithArgs("--disable=servicelb")
            .WithArgs("--disable=metrics-server")

            // Reduce log verbosity — k3s writes everything to stderr which DCP surfaces
            // as "error" level entries in the dashboard log viewer.
            .WithArgs("-v", "0")
            .WithArgs("--kube-apiserver-arg=v=0")
            .WithArgs("--kube-controller-manager-arg=v=0")
            .WithArgs("--kube-scheduler-arg=v=0")
            // Suppress kubelet INFO-level noise including the harmless cgroupsv2 race warning
            // "Failed to kill all the processes attached to cgroup / os: process not initialized".
            // This is a known benign race condition in Docker-in-Docker: the kubelet tries to
            // force-kill pod cgroup processes that are already dead. The cgroup is still cleaned
            // up correctly; only the redundant kill attempt fails.
            .WithArgs("--kubelet-arg=v=0")

            // ── API server endpoint ───────────────────────────────────────────
            .WithHttpsEndpoint(
                targetPort: 6443,
                port: apiServerPort,
                name: K3sClusterResource.ApiServerEndpointName)

            // ── Docker / container runtime flags (mirrors k3d) ────────────────
            // Privileged mode is mandatory for iptables, network namespaces, and cgroups.
            .WithContainerRuntimeArgs("--privileged")
            // k3d uses Docker's --init (tini) so that k3s's child processes are properly
            // reaped and signals are forwarded correctly. Without it, zombie processes
            // accumulate and shutdown becomes unreliable.
            .WithContainerRuntimeArgs("--init")
            // Use the host user namespace — required when Docker is configured with userns-remap;
            // a no-op otherwise. k3d always passes this flag.
            .WithContainerRuntimeArgs("--userns=host")
            // Share the host's (Docker Desktop VM's) cgroup namespace instead of creating
            // a new one. Without this, the k3s kubelet fails to create the "kubepods" cgroup
            // hierarchy because the new isolated namespace has domain controllers in an invalid
            // state for cgroupsv2. k3d always passes --cgroupns=host for this reason.
            .WithContainerRuntimeArgs("--cgroupns=host")
            // Bind-mount the cgroup filesystem from the Docker Desktop VM into the container
            // as read-write. With --cgroupns=host the container sees the host's cgroup namespace,
            // but the mount is still read-only by default; making it rw lets the kubelet create
            // sub-cgroups for pods (kubepods/besteffort/...). k3d always mounts this as rw.
            .WithContainerRuntimeArgs("--volume=/sys/fs/cgroup:/sys/fs/cgroup:rw")
            // tmpfs mounts for runtime sockets and PIDs — same as k3d defaults.
            .WithContainerRuntimeArgs("--tmpfs=/run", "--tmpfs=/var/run")

            // ── Environment ───────────────────────────────────────────────────
            // Set an explicit cluster token (k3d always sets K3S_TOKEN).
            .WithEnvironment("K3S_TOKEN", $"aspire-k3s-{name}-token")
            // World-readable kubeconfig so docker exec can read it without root.
            .WithEnvironment("K3S_KUBECONFIG_MODE", "644")

            .WithIconName("Kubernetes")
            .WithHttpsDeveloperCertificate();

        if (options.ClusterCidr is not null)
        {
            resourceBuilder.WithArgs($"--cluster-cidr={options.ClusterCidr}");
        }

        if (options.ServiceCidr is not null)
        {
            resourceBuilder.WithArgs($"--service-cidr={options.ServiceCidr}");
        }

        foreach (var component in options.DisabledComponents)
        {
            resourceBuilder.WithArgs($"--disable={component}");
        }

        foreach (var arg in options.ExtraArgs)
        {
            resourceBuilder.WithArgs(arg);
        }

        // Create agent nodes specified via options.AgentCount.
        // Agents use DCP DNS: K3S_URL=https://{name}:6443 resolves to the server container.
        // NO WaitFor — k3s agent retries indefinitely until the server is reachable.
        // This avoids a deadlock where the cluster health check waits for nodes to be Ready
        // while nodes wait for the cluster to be healthy.
        for (var i = 0; i < options.AgentCount; i++)
        {
            resource.AgentCount++;
            var agentName = $"{name}-agent-{i}";
            var agentResource = new K3sAgentResource(agentName, resource);
            resource.AddAgentResource(agentResource);

            builder.AddResource(agentResource)
                .WithImage(K3sContainerImageTags.Image, tag)
                .WithImageRegistry(K3sContainerImageTags.Registry)
                .WithContainerFiles("/", [new ContainerFile
                {
                    Name = "aspire-k3s-entrypoint.sh",
                    Contents = K3sInitEntrypointScript,
                    Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                         | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                         | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
                }])
                .WithEntrypoint("/bin/sh")
                .WithArgs("/aspire-k3s-entrypoint.sh")
                .WithArgs("agent")
                .WithArgs("-v", "0")
                .WithArgs("--kubelet-arg=v=0")
                .WithEnvironment("K3S_URL", $"https://{name}:6443")
                .WithEnvironment("K3S_TOKEN", $"aspire-k3s-{name}-token")
                .WithEnvironment("K3S_NODE_NAME", agentName)
                .WithContainerRuntimeArgs("--privileged")
                .WithContainerRuntimeArgs("--init")
                .WithContainerRuntimeArgs("--userns=host")
                .WithContainerRuntimeArgs("--cgroupns=host")
                .WithContainerRuntimeArgs("--volume=/sys/fs/cgroup:/sys/fs/cgroup:rw")
                .WithContainerRuntimeArgs("--tmpfs=/run", "--tmpfs=/var/run")
                .ExcludeFromManifest()
                .WithInitialState(new CustomResourceSnapshot
                {
                    ResourceType = "K3s Agent",
                    State = KnownResourceStates.Starting,
                    Properties = [new ResourcePropertySnapshot("Cluster", name)],
                });
        }

        resourceBuilder.WithHealthCheck($"k3s_{name}_ready");

        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            $"k3s_{name}_ready",
            sp => new K3sReadinessHealthCheck(resource, resource.ApiEndpoint),
            failureStatus: HealthStatus.Unhealthy,
            tags: null));

        // Postgres pattern: the parent cluster's ResourceReadyEvent drives ALL registered
        // child lifecycles — both HelmReleases and K8sManifests — in parallel.
        builder.Eventing.Subscribe<ResourceReadyEvent>(resource, (@event, ct) =>
        {
            var appModel = @event.Services.GetRequiredService<DistributedApplicationModel>();
            var notifications = @event.Services.GetRequiredService<ResourceNotificationService>();
            var loggerService = @event.Services.GetRequiredService<ResourceLoggerService>();

            // Start all Helm release installs concurrently.
            foreach (var release in appModel.Resources
                .OfType<HelmReleaseResource>()
                .Where(r => ReferenceEquals(r.Parent, resource)))
            {
                var logger = loggerService.GetLogger(release);
                _ = Task.Run(() => K3sHelmBuilderExtensions.RunReleaseAsync(
                    release, resource, notifications, logger, ct), ct);
            }

            // Start all manifest applies concurrently.
            foreach (var manifest in appModel.Resources
                .OfType<K8sManifestResource>()
                .Where(m => ReferenceEquals(m.Parent, resource)))
            {
                var logger = loggerService.GetLogger(manifest);
                _ = Task.Run(() => K3sManifestBuilderExtensions.RunManifestAsync(
                    manifest, resource, notifications, logger, ct), ct);
            }

            return Task.CompletedTask;
        });

        return resourceBuilder;
    }

    /// <summary>Overrides the k3s server image version.</summary>
    [AspireExport("withK3sVersion", Description = "Overrides the k3s server image version")]
    public static IResourceBuilder<K3sClusterResource> WithK3sVersion(
        this IResourceBuilder<K3sClusterResource> builder,
        string tag)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        return builder.WithImageTag(tag);
    }

    /// <summary>Sets the pod subnet CIDR (<c>--cluster-cidr</c>).</summary>
    [AspireExport("withPodSubnet", Description = "Sets the pod subnet CIDR for the k3s cluster")]
    public static IResourceBuilder<K3sClusterResource> WithPodSubnet(
        this IResourceBuilder<K3sClusterResource> builder,
        string cidr)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(cidr);

        return builder.WithArgs($"--cluster-cidr={cidr}");
    }

    /// <summary>Sets the service subnet CIDR (<c>--service-cidr</c>).</summary>
    [AspireExport("withServiceSubnet", Description = "Sets the service subnet CIDR for the k3s cluster")]
    public static IResourceBuilder<K3sClusterResource> WithServiceSubnet(
        this IResourceBuilder<K3sClusterResource> builder,
        string cidr)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(cidr);

        return builder.WithArgs($"--service-cidr={cidr}");
    }

    /// <summary>Disables a built-in k3s component (e.g. <c>traefik</c>).</summary>
    [AspireExport("withDisabledComponent", Description = "Disables a built-in k3s component")]
    public static IResourceBuilder<K3sClusterResource> WithDisabledComponent(
        this IResourceBuilder<K3sClusterResource> builder,
        string component)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(component);

        return builder.WithArgs($"--disable={component}");
    }

    /// <summary>Appends a raw argument to the <c>k3s server</c> command.</summary>
    [AspireExport("withExtraArg", Description = "Appends a raw argument to the k3s server command")]
    public static IResourceBuilder<K3sClusterResource> WithExtraArg(
        this IResourceBuilder<K3sClusterResource> builder,
        string arg)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(arg);

        return builder.WithArgs(arg);
    }

    /// <summary>
    /// Adds a named volume for the k3s cluster data directory (<c>/var/lib/rancher/k3s</c>)
    /// so the cluster state (SQLite database, certificates, kubeconfig) survives AppHost restarts.
    /// </summary>
    /// <param name="builder">The cluster resource builder.</param>
    /// <param name="name">
    /// The volume name. When <see langword="null"/>, an auto-generated name is used in the form
    /// <c>{appName}-{sha256}-{resourceName}-data</c> — the same scheme used by
    /// <c>PostgresServerResource.WithDataVolume</c> and all other Aspire hosting integrations.
    /// </param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<K3sClusterResource> WithDataVolume(
        this IResourceBuilder<K3sClusterResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/lib/rancher/k3s")
            // Auto-restart on crash so a persistent cluster survives transient failures
            // without requiring AppHost intervention. Docker will not restart the container
            // on explicit stop (DCP shutdown), only on unexpected exits.
            .WithContainerRuntimeArgs("--restart=unless-stopped");
    }

    /// <summary>
    /// Injects the k3s kubeconfig into <paramref name="destination"/> as a
    /// <c>KUBECONFIG_DATA</c> environment variable (base-64-encoded YAML) — no files are written.
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="ContainerResource"/>s (Auto or InlineData) receive the
    ///     <em>container-network</em> kubeconfig
    ///     (<c>server: https://{resourceName}:6443</c>).
    ///   </item>
    ///   <item>
    ///     Projects and executables (Auto or HostPath) receive the
    ///     <em>host</em> kubeconfig
    ///     (<c>server: https://localhost:{allocatedPort}</c>).
    ///   </item>
    /// </list>
    /// Consuming code reads the variable and builds a client without touching disk:
    /// <code>
    /// var bytes  = Convert.FromBase64String(Environment.GetEnvironmentVariable("KUBECONFIG_DATA")!);
    /// using var stream = new MemoryStream(bytes);
    /// var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(stream);
    /// </code>
    /// The variable is populated only after the cluster health check passes; use
    /// <c>WaitFor(cluster)</c> on the dependent resource to guarantee ordering.
    /// </summary>
    [AspireExport("withReference", Description = "Injects kubeconfig credentials into the dependent resource")]
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> destination,
        IResourceBuilder<K3sClusterResource> source,
        KubeconfigInjectionStrategy strategy = KubeconfigInjectionStrategy.Auto)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);

        var cluster = source.Resource;

        return destination.WithEnvironment(ctx =>
        {
            // Select the right kubeconfig variant based on where the resource runs:
            //   • ContainerResource  → container-network URL (reaches API server via DCP DNS)
            //   • Project/Executable → host URL (reaches API server via localhost port-mapping)
            var useContainerVariant =
                strategy == KubeconfigInjectionStrategy.ContainerNetwork
                || (strategy == KubeconfigInjectionStrategy.Auto
                    && destination.Resource is ContainerResource);

            var cfg = useContainerVariant
                ? cluster.ContainerKubeconfig
                : cluster.AdminKubeconfig;

            if (cfg is not null)
            {
                var yaml = KubernetesYaml.Serialize(cfg);
                ctx.EnvironmentVariables["KUBECONFIG_DATA"] =
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(yaml));
            }
        });
    }

    /// <summary>
    /// Serialises the admin kubeconfig (<c>server: https://localhost:{port}</c>) to YAML.
    /// Returns <see langword="null"/> if the health check has not yet populated the config.
    /// </summary>
    internal static string? GetAdminKubeconfigYaml(K3sClusterResource cluster) =>
        cluster.AdminKubeconfig is null
            ? null
            : KubernetesYaml.Serialize(cluster.AdminKubeconfig);

    /// <summary>
    /// Sets the container lifetime for the k3s cluster <em>and all its agent nodes</em>.
    /// When <see cref="ContainerLifetime.Persistent"/> is used the agents must also be
    /// persistent — otherwise they are recreated on every AppHost restart while the server
    /// retains its state, causing the node re-join sequence to fail.
    /// </summary>
    public static IResourceBuilder<K3sClusterResource> WithLifetime(
        this IResourceBuilder<K3sClusterResource> builder,
        ContainerLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Apply to the cluster container (identical to the built-in generic WithLifetime).
        builder.WithAnnotation(
            new ContainerLifetimeAnnotation { Lifetime = lifetime },
            ResourceAnnotationMutationBehavior.Replace);

        // Propagate to every agent — they share the same cluster state volume and must
        // have the same lifetime so nodes survive AppHost restarts in sync with the server.
        foreach (var agent in builder.Resource.AgentResources)
        {
            var existing = agent.Annotations.OfType<ContainerLifetimeAnnotation>().ToList();
            foreach (var ann in existing)
            {
                agent.Annotations.Remove(ann);
            }

            agent.Annotations.Add(new ContainerLifetimeAnnotation { Lifetime = lifetime });
        }

        return builder;
    }

    // cgroupsv2 fix adapted from moby/moby (Apache-2.0, used with permission by k3d).
    // See: https://github.com/k3d-io/k3d/blob/main/pkg/types/fixes/assets/k3d-entrypoint-cgroupv2.sh
    private const string K3sInitEntrypointScript = """
        #!/bin/sh
        # Aspire k3s init entrypoint — adapted from k3d (https://github.com/k3d-io/k3d)
        # cgroupsv2 fix adapted from moby/moby (Apache-2.0), used with permission.

        # Make mountpoints recursively shared — required for volume propagation in Docker-in-Docker.
        mount --make-rshared / 2>/dev/null || true

        # cgroupsv2: evacuate root cgroup so k3s kubelet can create pod sub-cgroups.
        # Without this, writing to cgroup.subtree_control fails with EBUSY because
        # init processes still live in the root cgroup.
        if [ -f /sys/fs/cgroup/cgroup.controllers ]; then
            mkdir -p /sys/fs/cgroup/init
            if command -v xargs >/dev/null 2>&1; then
                xargs -rn1 < /sys/fs/cgroup/cgroup.procs > /sys/fs/cgroup/init/cgroup.procs 2>/dev/null || true
            else
                busybox xargs -rn1 < /sys/fs/cgroup/cgroup.procs > /sys/fs/cgroup/init/cgroup.procs 2>/dev/null || true
            fi
            sed -e 's/ / +/g' -e 's/^/+/' < /sys/fs/cgroup/cgroup.controllers \
                > /sys/fs/cgroup/cgroup.subtree_control 2>/dev/null || true
        fi

        exec k3s "$@"
        """;
}

#pragma warning restore ASPIREATS001
#pragma warning restore ASPIRECERTIFICATES001
