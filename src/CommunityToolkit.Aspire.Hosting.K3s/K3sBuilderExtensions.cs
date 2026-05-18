using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

        var resource = new K3sClusterResource(name)
        {
            HelmImageInfo = (options.HelmRegistry, options.HelmImage, options.HelmTag),
            KubectlImageInfo = (options.KubectlRegistry, options.KubectlImage, options.KubectlTag),
        };
        var tag = options.ImageTag ?? K3sContainerImageTags.Tag;

        // ── Kubeconfig directory on the host ──────────────────────────────────
        // AppHostDirectory/.k3s/{name}/ holds three sub-directories:
        //   cluster/  — bind-mounted into the k3s container; k3s writes kubeconfig.yaml here
        //   local/    — rewritten by the health check with server: https://localhost:{port}
        //   container/ — rewritten by the health check with server: https://{name}:6443
        var kubeconfigDir = Path.Combine(builder.AppHostDirectory, ".k3s", name);
        var clusterDir = Path.Combine(kubeconfigDir, "cluster");
        Directory.CreateDirectory(clusterDir);

        resource.KubeconfigDirectory = kubeconfigDir;

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
            // Suppress kubelet INFO-level noise including the harmless cgroupsv2 race warning.
            .WithArgs("--kubelet-arg=v=0")

            // ── API server endpoint ───────────────────────────────────────────
            // Proxy support must be disabled: the kubeconfig embeds the k3s server CA cert
            // for TLS validation. An Aspire HTTPS proxy would present its own certificate,
            // causing Kubernetes client TLS validation to fail on every connection.
            .WithHttpsEndpoint(
                targetPort: 6443,
                port: apiServerPort,
                name: K3sClusterResource.ApiServerEndpointName,
                isProxied: false)

            // ── Docker / container runtime flags (mirrors k3d) ────────────────
            .WithContainerRuntimeArgs("--privileged")
            .WithContainerRuntimeArgs("--init")
            .WithContainerRuntimeArgs("--userns=host")
            .WithContainerRuntimeArgs("--cgroupns=host")
            .WithContainerRuntimeArgs("--volume=/sys/fs/cgroup:/sys/fs/cgroup:rw")
            .WithContainerRuntimeArgs("--tmpfs=/run", "--tmpfs=/var/run")

            // ── Kubeconfig bind-mount ─────────────────────────────────────────
            // Mounts AppHostDirectory/.k3s/{name}/cluster/ into the container so k3s
            // writes its kubeconfig into a host-accessible directory.
            // K3S_KUBECONFIG_OUTPUT tells k3s where to write the kubeconfig file.
            // The health check polls File.Exists on the host side — no docker exec needed.
            .WithBindMount(clusterDir, "/tmp/k3s-kubeconfig")

            // ── Environment ───────────────────────────────────────────────────
            .WithEnvironment("K3S_TOKEN", $"aspire-k3s-{name}-token")
            .WithEnvironment("K3S_KUBECONFIG_MODE", "644")
            .WithEnvironment("K3S_KUBECONFIG_OUTPUT", "/tmp/k3s-kubeconfig/kubeconfig.yaml")

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

        // The cluster's ResourceReadyEvent drives service endpoint port-forwards.
        // HelmReleaseResource and K8sManifestResource containers are managed directly
        // by DCP — they WaitFor the cluster and exit when their work completes.
        builder.Eventing.Subscribe<ResourceReadyEvent>(resource, (@event, ct) =>
        {
            var appModel = @event.Services.GetRequiredService<DistributedApplicationModel>();
            var notifications = @event.Services.GetRequiredService<ResourceNotificationService>();
            var loggerService = @event.Services.GetRequiredService<ResourceLoggerService>();

            // Start all service endpoint forwarders concurrently.
            foreach (var ep in appModel.Resources
                .OfType<K3sServiceEndpointResource>()
                .Where(e => ReferenceEquals(e.Parent, resource)))
            {
                var logger = loggerService.GetLogger(ep);
                _ = Task.Run(() => K3sServiceEndpointExtensions.RunEndpointAsync(
                    ep, resource, notifications, logger, ct), ct);
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

        // Update the server image tag.
        builder.WithImageTag(tag);

        // Sync all agent nodes to the same tag — mismatched server/agent versions can
        // break node joins and exceed Kubernetes' supported ±1 minor version skew.
        foreach (var agent in builder.Resource.AgentResources)
        {
            var existing = agent.Annotations.OfType<ContainerImageAnnotation>().FirstOrDefault();
            if (existing is not null)
            {
                agent.Annotations.Remove(existing);
                agent.Annotations.Add(new ContainerImageAnnotation
                {
                    Image = existing.Image,
                    Tag = tag,
                    Registry = existing.Registry,
                });
            }
        }

        return builder;
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
    [AspireExport("withDataVolume", Description = "Adds a named volume for the k3s cluster data directory so state survives AppHost restarts")]
    public static IResourceBuilder<K3sClusterResource> WithDataVolume(
        this IResourceBuilder<K3sClusterResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/lib/rancher/k3s");
    }

    /// <summary>
    /// Injects the k3s kubeconfig into <paramref name="destination"/> so it can authenticate
    /// to the cluster. The injection method is selected automatically based on the resource type:
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="ContainerResource"/>s receive a physical kubeconfig file copied to
    ///     <c>/var/k3s/kubeconfig.yaml</c> (container-network variant,
    ///     <c>server: https://{resourceName}:6443</c>). <c>KUBECONFIG=/var/k3s/kubeconfig.yaml</c>
    ///     is set automatically so all standard Kubernetes tooling (<c>kubectl</c>, <c>helm</c>,
    ///     KubernetesClient SDK) works without any custom bootstrap code.
    ///   </item>
    ///   <item>
    ///     Projects and executables receive <c>KUBECONFIG=&lt;host path&gt;/local/kubeconfig.yaml</c>
    ///     pointing directly to a file on the host filesystem.
    ///   </item>
    /// </list>
    /// Both files are written by the health check after all nodes reach <c>Ready</c> state.
    /// Use <c>WaitFor(cluster)</c> on the dependent resource to guarantee the files exist
    /// before the resource starts.
    /// </summary>
    [AspireExport("withReference", Description = "Injects kubeconfig credentials into the dependent resource")]
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> destination,
        IResourceBuilder<K3sClusterResource> source)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);

        var cluster = source.Resource;

        if (destination.Resource is ContainerResource)
        {
            // Containers get a bind-mount of the container/ kubeconfig directory at /var/k3s.
            // ContainerMountAnnotation is added directly to bypass the T : ContainerResource
            // constraint on WithBindMount — the annotation is equivalent.
            //
            // Bind-mount (not file copy) is used for the same reason as in the helm and
            // kubectl installer containers: if the cluster is recreated while a container is
            // running, the new kubeconfig appears automatically without restarting the container.
            var containerKubeconfigDir = Path.Combine(cluster.KubeconfigDirectory!, "container");
            Directory.CreateDirectory(containerKubeconfigDir);

            destination.Resource.Annotations.Add(
                new ContainerMountAnnotation(
                    containerKubeconfigDir,
                    "/var/k3s",
                    ContainerMountType.BindMount,
                    isReadOnly: true));

            return destination.WithEnvironment("KUBECONFIG", "/var/k3s/kubeconfig.yaml");
        }

        // Projects and executables: KUBECONFIG points to the host-accessible local kubeconfig.
        // This file is regenerated on every AppHost start (port may change).
        return destination.WithEnvironment(ctx =>
        {
            if (cluster.KubeconfigDirectory is null) return;
            var path = Path.Combine(cluster.KubeconfigDirectory, "local", "kubeconfig.yaml");
            ctx.EnvironmentVariables["KUBECONFIG"] = path;
        });
    }

    /// <summary>
    /// Sets the container lifetime for the k3s cluster <em>and all its agent nodes</em>.
    /// </summary>
    [AspireExport("withLifetime", Description = "Sets the container lifetime for the k3s cluster and all its agent nodes")]
    public static IResourceBuilder<K3sClusterResource> WithLifetime(
        this IResourceBuilder<K3sClusterResource> builder,
        ContainerLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithAnnotation(
            new ContainerLifetimeAnnotation { Lifetime = lifetime },
            ResourceAnnotationMutationBehavior.Replace);

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

    /// <summary>
    /// Returns the path to the <c>local/kubeconfig.yaml</c> file for this cluster,
    /// used by helm and manifest runners that invoke host-side tools.
    /// Returns <see langword="null"/> if the cluster directory is not yet configured.
    /// </summary>
    internal static string? GetLocalKubeconfigPath(K3sClusterResource cluster) =>
        cluster.KubeconfigDirectory is null
            ? null
            : Path.Combine(cluster.KubeconfigDirectory, "local", "kubeconfig.yaml");

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
