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
    /// Adds a k3s Kubernetes cluster to the distributed application.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">
    /// The resource name. Also used as the DNS hostname by which containers in the DCP network
    /// reach the cluster's API server (e.g. <c>https://{name}:6443</c>).
    /// </param>
    /// <param name="apiServerPort">
    /// Host port to bind the Kubernetes API server (port 6443) to.
    /// When <see langword="null"/> (the default) a random available port is assigned.
    /// </param>
    /// <param name="agentCount">
    /// Number of k3s agent (worker) nodes to add. When <see langword="null"/> (the default)
    /// a single-node cluster is created — the server node acts as both control-plane and worker.
    /// Equivalent to calling <see cref="WithAgentCount"/> on the returned builder.
    /// </param>
    /// <returns>A builder for the <see cref="K3sClusterResource"/>.</returns>
    /// <remarks>
    /// <para>
    /// The cluster runs as a privileged container using the <c>rancher/k3s</c> image.
    /// No host-side <c>kubectl</c>, <c>helm</c>, or <c>k3s</c> binaries are required.
    /// </para>
    /// <para>
    /// Three kubeconfig variants are written to <c>{AppHostDirectory}/.k3s/{name}/</c>
    /// when the cluster becomes ready:
    /// <list type="bullet">
    ///   <item><c>local/kubeconfig.yaml</c> — injected into host processes via <c>KUBECONFIG</c>.</item>
    ///   <item><c>container/kubeconfig.yaml</c> — bind-mounted into containers via <c>KUBECONFIG</c>.</item>
    /// </list>
    /// Call <c>WithReference(cluster)</c> on a dependent resource builder to inject
    /// these credentials automatically.
    /// </para>
    /// <para>
    /// All other cluster options are available as fluent builder methods:
    /// <see cref="WithK3sVersion"/>, <see cref="WithAgentCount"/>, <see cref="WithPodSubnet"/>,
    /// <see cref="WithServiceSubnet"/>, <see cref="WithDisabledComponent"/>,
    /// <see cref="WithExtraArg"/>, <see cref="WithDataVolume"/>, <see cref="WithHelmImage"/>,
    /// <see cref="WithKubectlImage"/>, and <see cref="WithLifetime"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="name"/> is <see langword="null"/>.
    /// </exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> AddK3sCluster(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? apiServerPort = null,
        int? agentCount = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var resource = new K3sClusterResource(name);
        var tag = K3sContainerImageTags.Tag;

        // ── Kubeconfig directory on the host ──────────────────────────────────
        // AppHostDirectory/.k3s/{name}/ holds three sub-directories:
        //   cluster/  — bind-mounted into the k3s container; k3s writes kubeconfig.yaml here
        //   local/    — rewritten by the health check with server: https://localhost:{port}
        //   container/ — rewritten by the health check with server: https://{name}:6443
        var kubeconfigDir = Path.Combine(builder.AppHostDirectory, ".k3s", name);
        var clusterDir = Path.Combine(kubeconfigDir, "cluster");
        Directory.CreateDirectory(clusterDir);

        // Pre-create placeholder files for all bind-mount sources. Docker creates a
        // DIRECTORY at the source path when the file does not yet exist, which then
        // prevents the health check from writing the real kubeconfig atomically.
        // The health check overwrites these placeholders once the cluster is ready.
        EnsureKubeconfigPlaceholder(Path.Combine(kubeconfigDir, "container", "kubeconfig.yaml"));
        EnsureKubeconfigPlaceholder(Path.Combine(kubeconfigDir, "local", "kubeconfig.yaml"));

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
                Mode = K3sFileHelpers.ExecutableScriptMode,
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
            // Declared as HTTPS so the allocated host port appears in the Aspire dashboard
            // with the correct scheme. Aspire's HTTP proxy does not intercept raw TLS TCP
            // connections, so Kubernetes clients validate the k3s server CA cert directly
            // without any proxy interference.
            .WithHttpsEndpoint(
                targetPort: 6443,
                port: apiServerPort,
                name: K3sClusterResource.ApiServerEndpointName)

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

            .WithIconName("Kubernetes");

        // Create agent nodes if agentCount was supplied directly to AddK3sCluster.
        if (agentCount is > 0)
            AddAgentNodes(resourceBuilder, agentCount.Value, tag);

        resourceBuilder.WithHealthCheck($"k3s_{name}_ready");

        // Register as a singleton instance so the cached Kubernetes client and kubeconfig
        // state survive across health-check ticks. Using a factory (sp => new ...) would
        // create a fresh instance on every check, making _cachedClient dead state and
        // leaking a Kubernetes/HttpClient on every tick.
        var healthCheck = new K3sReadinessHealthCheck(resource);
        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            $"k3s_{name}_ready",
            _ => healthCheck,
            failureStatus: HealthStatus.Unhealthy,
            tags: null));

        // BeforeStartEvent: apply KUBECONFIG and service-URL injections declared via
        // WithReference. The cluster owns this behavior — it knows what to inject and when.
        // Processing is deferred to BeforeStartEvent so that resources can be wired up in
        // any order in Program.cs without worrying about whether the cluster is configured yet.
        builder.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
        {
            var appModel = evt.Services.GetRequiredService<DistributedApplicationModel>();

            foreach (var dependent in appModel.Resources)
            {
                // ── Container KUBECONFIG override ─────────────────────────────
                // Standard WithReference(cluster) already injected KUBECONFIG=<local-path>
                // for all resource types (via IResourceWithConnectionString). For containers
                // we additionally need: a file-level bind-mount of the container-network
                // kubeconfig variant, plus an env override that points to it.
                //
                // Detect via the ResourceRelationshipAnnotation that standard WithReference
                // adds when called with our cluster (which implements IResourceWithConnectionString).
                bool referencesThisCluster = dependent.Annotations
                    .OfType<ResourceRelationshipAnnotation>()
                    .Any(a => ReferenceEquals(a.Resource, resource));

                if (referencesThisCluster && dependent is ContainerResource)
                {
                    ApplyKubeconfigContainerOverride(dependent, resource);
                }

                // ── Service-URL container override ────────────────────────────
                // K3sServiceEndpointResource implements IResourceWithConnectionString, so
                // standard WithReference injects services__ep__url=http://localhost:PORT for
                // all resource types. For containers the URL must use host.docker.internal.
                // Detect via the ResourceRelationshipAnnotation that standard WithReference
                // adds when called with our endpoint (which implements IResourceWithConnectionString).
                var endpointRefs = dependent.Annotations
                    .OfType<ResourceRelationshipAnnotation>()
                    .Select(a => a.Resource)
                    .OfType<K3sServiceEndpointResource>()
                    .Where(ep => ReferenceEquals(ep.Parent, resource))
                    .ToList();

                if (endpointRefs.Count > 0 && dependent is ContainerResource)
                {
                    // --add-host is needed once per container regardless of how many
                    // endpoints are referenced.
                    dependent.Annotations.Add(
                        new ContainerRuntimeArgsCallbackAnnotation(
                            args => args.Add("--add-host=host.docker.internal:host-gateway")));
                }

                foreach (var ep in endpointRefs)
                    ApplyServiceUrlContainerOverride(dependent, ep);
            }

            return Task.CompletedTask;
        });

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

    /// <summary>
    /// Sets the k3s image version used by the cluster server and all its agent nodes.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="tag">
    /// The k3s container image tag, e.g. <c>v1.32.3-k3s1</c>.
    /// Must follow the <c>v{major}.{minor}.{patch}-k3s{n}</c> format.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// All agent nodes are immediately synced to the same tag to prevent version skew
    /// beyond the Kubernetes-supported ±1 minor version limit. The image tag is part of
    /// the DCP container identity, so synchronisation must happen at configuration time.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tag"/> is <see langword="null"/> or whitespace.</exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithK3sVersion(
        this IResourceBuilder<K3sClusterResource> builder,
        string tag)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        builder.WithImageTag(tag);

        // Sync agents immediately — DCP uses the ContainerImageAnnotation to compute
        // container identity, so the tag must be set at configuration time, not deferred
        // to BeforeStartEvent (which fires after DCP has already determined the identity).
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

    /// <summary>
    /// Sets the CIDR range for pod IP addresses (<c>--cluster-cidr</c>).
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="cidr">The pod subnet in CIDR notation, e.g. <c>10.42.0.0/16</c>.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="cidr"/> is <see langword="null"/> or whitespace.</exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithPodSubnet(
        this IResourceBuilder<K3sClusterResource> builder,
        string cidr)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(cidr);

        return builder.WithArgs($"--cluster-cidr={cidr}");
    }

    /// <summary>
    /// Sets the CIDR range for Service cluster IPs (<c>--service-cidr</c>).
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="cidr">The service subnet in CIDR notation, e.g. <c>10.43.0.0/16</c>.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="cidr"/> is <see langword="null"/> or whitespace.</exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithServiceSubnet(
        this IResourceBuilder<K3sClusterResource> builder,
        string cidr)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(cidr);

        return builder.WithArgs($"--service-cidr={cidr}");
    }

    /// <summary>
    /// Disables a built-in k3s component (<c>--disable=&lt;component&gt;</c>).
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="component">
    /// The component name to disable. Common values include <c>traefik</c>,
    /// <c>servicelb</c>, <c>metrics-server</c>, <c>coredns</c>, and <c>local-storage</c>.
    /// Call this method multiple times to disable more than one component.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="component"/> is <see langword="null"/> or whitespace.</exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithDisabledComponent(
        this IResourceBuilder<K3sClusterResource> builder,
        string component)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(component);

        return builder.WithArgs($"--disable={component}");
    }

    /// <summary>
    /// Appends a raw argument to the <c>k3s server</c> command line.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="arg">
    /// The raw argument to append, e.g. <c>--write-kubeconfig-mode=644</c>.
    /// Call this method multiple times to append additional arguments.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Use <see cref="WithDisabledComponent"/> or the dedicated CIDR methods when possible.
    /// This method is intended for flags that have no dedicated helper.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="arg"/> is <see langword="null"/> or whitespace.</exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithExtraArg(
        this IResourceBuilder<K3sClusterResource> builder,
        string arg)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(arg);

        return builder.WithArgs(arg);
    }

    /// <summary>
    /// Mounts a named Docker volume at the k3s data directory so cluster state persists across
    /// AppHost restarts.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="name">
    /// Optional volume name. When <see langword="null"/> (the default) a name is generated
    /// from the application and resource names.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// The volume covers <c>/var/lib/rancher/k3s</c>, which contains the SQLite database,
    /// TLS certificates, and kubeconfig. Without this volume the cluster starts fresh on
    /// every AppHost launch. Combine with <c>ContainerLifetime.Persistent</c> on the cluster
    /// resource and its dependent Helm releases to avoid re-installing charts on every start.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithDataVolume(
        this IResourceBuilder<K3sClusterResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Idempotent: remove any existing data-volume annotation at this target so that
        // calling WithDataVolume twice does not produce duplicate mounts that Docker rejects.
        var existing = builder.Resource.Annotations
            .OfType<ContainerMountAnnotation>()
            .FirstOrDefault(m => m.Target == "/var/lib/rancher/k3s");
        if (existing is not null)
            builder.Resource.Annotations.Remove(existing);

        return builder
            .WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/lib/rancher/k3s");
    }

    /// <summary>
    /// Sets the number of k3s agent (worker) nodes to add to the cluster.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="count">
    /// The number of agent nodes. Zero or greater. Defaults to <c>0</c> (single-node cluster —
    /// the server node acts as both control-plane and worker).
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Agent nodes connect to the server via DCP DNS (<c>https://{name}:6443</c>) and use
    /// k3s's built-in retry loop, so no explicit <c>WaitFor</c> is needed. The cluster health
    /// check waits for <c>1 + count</c> nodes to reach <c>Ready</c> state before reporting
    /// healthy. Use <see cref="WithLifetime"/> with <see cref="ContainerLifetime.Persistent"/>
    /// to keep agents alive across AppHost restarts and avoid node password hash mismatches.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithAgentCount(
        this IResourceBuilder<K3sClusterResource> builder,
        int count)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(count));

        if (count == 0) return builder;

        // Use the tag already set on the cluster (either the default or from WithK3sVersion).
        var tag = builder.Resource.Annotations
            .OfType<ContainerImageAnnotation>()
            .FirstOrDefault()?.Tag ?? K3sContainerImageTags.Tag;

        AddAgentNodes(builder, count, tag);
        return builder;
    }

    /// <summary>
    /// Overrides the container image used to run <c>helm upgrade --install</c> for
    /// all <see cref="HelmReleaseResource"/> children of this cluster.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="tag">Image tag, e.g. <c>3.18.0</c>. <see langword="null"/> keeps the current value.</param>
    /// <param name="image">Image name, e.g. <c>alpine/helm</c>. <see langword="null"/> keeps the current value.</param>
    /// <param name="registry">Registry, e.g. <c>docker.io</c>. <see langword="null"/> keeps the current value.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithHelmImage(
        this IResourceBuilder<K3sClusterResource> builder,
        string? tag = null,
        string? image = null,
        string? registry = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var (r, i, t) = builder.Resource.HelmImageInfo;
        builder.Resource.HelmImageInfo = (registry ?? r, image ?? i, tag ?? t);
        return builder;
    }

    /// <summary>
    /// Overrides the container image used to run <c>kubectl apply</c> for all
    /// <see cref="K8sManifestResource"/> children of this cluster.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="tag">Image tag, e.g. <c>1.37.0</c>. <see langword="null"/> keeps the current value.</param>
    /// <param name="image">Image name, e.g. <c>alpine/kubectl</c>. <see langword="null"/> keeps the current value.</param>
    /// <param name="registry">Registry, e.g. <c>docker.io</c>. <see langword="null"/> keeps the current value.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithKubectlImage(
        this IResourceBuilder<K3sClusterResource> builder,
        string? tag = null,
        string? image = null,
        string? registry = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var (r, i, t) = builder.Resource.KubectlImageInfo;
        builder.Resource.KubectlImageInfo = (registry ?? r, image ?? i, tag ?? t);
        return builder;
    }

    /// <summary>
    /// Sets the container lifetime for the k3s cluster <em>and all its agent nodes</em>.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="lifetime">The container lifetime to apply.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Agent nodes are propagated immediately because DCP uses
    /// <see cref="ContainerLifetimeAnnotation"/> to compute container identity. Deferring
    /// propagation to <c>BeforeStartEvent</c> would be too late — DCP determines whether
    /// to reuse or recreate a persistent container before that event fires, so agents
    /// would lose their persistent identity and be recreated as new containers each run.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    [AspireExport]
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
            foreach (var ann in agent.Annotations.OfType<ContainerLifetimeAnnotation>().ToList())
                agent.Annotations.Remove(ann);
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

        # Raise inotify limits so multiple k3s instances (server + agents) don't exhaust
        # the kernel default (128 instances). Applies to the Docker VM / host kernel because
        # k3s containers run with --privileged --userns=host. Silently skipped on runtimes
        # where the write is not permitted.
        sysctl -w fs.inotify.max_user_instances=1024 2>/dev/null || true
        sysctl -w fs.inotify.max_user_watches=1048576 2>/dev/null || true

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

    // ── Agent node creation ───────────────────────────────────────────────────

    // Shared by AddK3sCluster (via options.AgentCount) and WithAgentCount.
    // Agents use DCP DNS (K3S_URL=https://{name}:6443) and retry until the server is
    // reachable — no WaitFor to avoid a deadlock where the cluster health check waits
    // for nodes to be Ready while nodes wait for the cluster to be healthy.
    private static void AddAgentNodes(
        IResourceBuilder<K3sClusterResource> clusterBuilder,
        int count,
        string tag)
    {
        var resource = clusterBuilder.Resource;
        var name = resource.Name;
        var startIndex = resource.AgentCount;

        for (var i = startIndex; i < startIndex + count; i++)
        {
            resource.AgentCount++;
            var agentName = $"{name}-agent-{i}";
            var agentResource = new K3sAgentResource(agentName, resource);
            resource.AddAgentResource(agentResource);

            clusterBuilder.ApplicationBuilder.AddResource(agentResource)
                .WithImage(K3sContainerImageTags.Image, tag)
                .WithImageRegistry(K3sContainerImageTags.Registry)
                .WithContainerFiles("/", [new ContainerFile
                {
                    Name = "aspire-k3s-entrypoint.sh",
                    Contents = K3sInitEntrypointScript,
                    Mode = K3sFileHelpers.ExecutableScriptMode,
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
    }

    // ── BeforeStartEvent helpers ──────────────────────────────────────────────

    // Called only for containers — standard WithReference(IResourceWithConnectionString) already
    // handles host processes by injecting KUBECONFIG=<local-path> via GetConnectionStringAsync().
    internal static void ApplyKubeconfigContainerOverride(IResource dependent, K3sClusterResource cluster)
    {
        // File-level bind-mount: only the kubeconfig YAML is visible inside the container.
        // Mounting the full container/ dir would expose kubectl's cache dirs and cause
        // concurrent-container cache corruption when multiple containers share the directory.
        var alreadyMounted = dependent.Annotations
            .OfType<ContainerMountAnnotation>()
            .Any(m => m.Target == K3sFileHelpers.ContainerKubeconfigPath);

        if (!alreadyMounted)
        {
            var containerKubeconfigFile = Path.Combine(
                cluster.KubeconfigDirectory!, "container", "kubeconfig.yaml");
            // Placeholder ensures Docker creates a file-level mount, not a directory.
            // AddK3sCluster already creates this; the guard handles late callers.
            EnsureKubeconfigPlaceholder(containerKubeconfigFile);

            dependent.Annotations.Add(new ContainerMountAnnotation(
                containerKubeconfigFile,
                K3sFileHelpers.ContainerKubeconfigPath,
                ContainerMountType.BindMount,
                isReadOnly: true));
        }

        // Override the KUBECONFIG env var that standard WithReference already set to the
        // local path — containers need the container-network variant mounted at a fixed path.
        // This callback runs after the standard one (added later in BeforeStartEvent), so last
        // write wins in the environment variable dictionary.
        dependent.Annotations.Add(new EnvironmentCallbackAnnotation(
            ctx => ctx.EnvironmentVariables["KUBECONFIG"] = K3sFileHelpers.ContainerKubeconfigPath));
    }

    // Creates an empty placeholder file so Docker's bind-mount sees a file at the source
    // path rather than creating a directory there. If a directory already exists from a
    // previous bad run it is removed first. The health check overwrites the placeholder
    // with the real kubeconfig content once the cluster is ready.
    //
    // FileMode.CreateNew atomically creates the file if it does not exist and throws
    // IOException if it already does. Swallowing that IOException makes the call
    // idempotent under concurrent invocations (multiple tests parallel on Windows).
    internal static void EnsureKubeconfigPlaceholder(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        if (Directory.Exists(filePath))
            Directory.Delete(filePath, recursive: true);
        try
        {
            using (new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        }
        catch (IOException)
        {
            // File already exists or was just created by a concurrent call — placeholder is in place.
        }
    }

    // Called only for containers. Standard WithReference(IResourceWithConnectionString)
    // already injected services__ep__url=http://localhost:PORT for host processes via
    // GetConnectionStringAsync(). This override switches the URL to host.docker.internal
    // so DCP-network containers can reach the port-forward listener.
    internal static void ApplyServiceUrlContainerOverride(IResource dependent, K3sServiceEndpointResource ep)
    {
        var scheme = ep.Scheme;
        var envKey = $"services__{ep.Name}__url";

        dependent.Annotations.Add(new EnvironmentCallbackAnnotation(ctx =>
        {
            if (ep.IsReady)
                ctx.EnvironmentVariables[envKey] = $"{scheme}://host.docker.internal:{ep.HostPort}";
        }));
    }
}

#pragma warning restore ASPIREATS001
#pragma warning restore ASPIRECERTIFICATES001
