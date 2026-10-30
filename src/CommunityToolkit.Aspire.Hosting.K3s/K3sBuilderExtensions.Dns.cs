using System.Text;
using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for the k3s cluster's DNS: making Aspire resources resolvable from pods,
/// and adding CoreDNS configuration.
/// </summary>
/// <remarks>
/// Pods resolve names through the cluster's CoreDNS, which forwards to the node's resolver. Inside
/// a container that resolver is Docker's embedded DNS on a loopback address, which pods cannot
/// reach — so a pod cannot find another Aspire container by name, nor resolve
/// <c>host.docker.internal</c>. These methods fill the gap through k3s's documented extension
/// point, the <c>coredns-custom</c> ConfigMap, which a helper container writes once the cluster is
/// ready and before any Helm release or manifest of the cluster runs.
/// </remarks>
public static partial class K3sDnsBuilderExtensions
{
    private const string CoreDnsConfigMap = "coredns-custom";
    private const string CustomizationsDirectory = "/coredns-custom";
    private const string HostAliasesFile = "aspire-host-aliases.server";

    /// <summary>
    /// Makes a container resource resolvable from every pod in the cluster.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="resource">
    /// The container resource pods should reach, such as a database the cluster's workloads use.
    /// </param>
    /// <param name="hostName">
    /// The name pods resolve it by. Defaults to the resource name — the name other containers on
    /// the Aspire network already use for it.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The container's address is looked up on the Aspire network every time the AppHost starts,
    /// and written into the cluster's CoreDNS as a <c>hosts</c> entry. Pods then connect to it
    /// directly on its container port — for example <c>postgres:5432</c> — the same address a
    /// container on the Aspire network uses.
    /// </para>
    /// <para>
    /// Every Helm release and manifest of the cluster waits for this to complete. A pod's own
    /// search domains are tried first, so a Service in the pod's namespace with the same name
    /// takes precedence.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="resource"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="hostName"/> is not a valid DNS name.</exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithHostAlias(
        this IResourceBuilder<K3sClusterResource> builder,
        IResourceBuilder<ContainerResource> resource,
        string? hostName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(resource);

        hostName ??= resource.Resource.Name;

        if (!DnsName().IsMatch(hostName))
        {
            throw new ArgumentException(
                $"'{hostName}' is not a DNS name: use letters, digits and hyphens, in labels separated by dots.",
                nameof(hostName));
        }

        builder.Resource.HostAliases[hostName] = resource.Resource;

        // Its address exists only once the container does.
        EnsureDnsResource(builder).WaitFor(resource);

        return builder;
    }

    /// <summary>
    /// Adds a file to the cluster's <c>coredns-custom</c> ConfigMap, which k3s's CoreDNS imports.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="fileName">
    /// The file name. A name ending in <c>.override</c> is imported into the cluster's main server
    /// block, for plugins such as <c>rewrite</c>; one ending in <c>.server</c> is a server block of
    /// its own, for a zone the cluster should answer differently.
    /// </param>
    /// <param name="content">The CoreDNS configuration the file holds.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// The ConfigMap is written by the cluster: a chart or manifest that also creates
    /// <c>coredns-custom</c> conflicts with it, so move that configuration here. Adding the same
    /// file name again replaces its content. CoreDNS is restarted after every write, because it
    /// reads these files when it starts.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="fileName"/> or <paramref name="content"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="fileName"/> does not end in <c>.override</c> or <c>.server</c>, contains a
    /// path, or is the name the cluster uses for host aliases.
    /// </exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithCoreDnsCustomization(
        this IResourceBuilder<K3sClusterResource> builder,
        string fileName,
        string content)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);

        if (!CoreDnsFileName().IsMatch(fileName))
        {
            throw new ArgumentException(
                $"'{fileName}' is not a coredns-custom file name: it must end in .override or .server and contain no path.",
                nameof(fileName));
        }

        if (string.Equals(fileName, HostAliasesFile, StringComparison.Ordinal))
            throw new ArgumentException($"'{HostAliasesFile}' holds the cluster's host aliases.", nameof(fileName));

        builder.Resource.CoreDnsCustomizations[fileName] = content;
        EnsureDnsResource(builder);

        return builder;
    }

    /// <summary>
    /// Makes <paramref name="dependent"/> wait for every container that prepares the cluster for
    /// workloads — its DNS, its certificate.
    /// </summary>
    internal static void WaitForClusterSetup(IResource dependent, K3sClusterResource cluster)
    {
        foreach (var setup in cluster.SetupResources)
        {
            if (!dependent.Annotations.OfType<WaitAnnotation>().Any(w => ReferenceEquals(w.Resource, setup)))
                dependent.Annotations.Add(new WaitAnnotation(setup, WaitType.WaitForCompletion));
        }
    }

    /// <summary>
    /// Registers <paramref name="setup"/> as a container every Helm release and manifest of the cluster
    /// waits for, including those declared before it.
    /// </summary>
    internal static void AddClusterSetup(IDistributedApplicationBuilder builder, K3sClusterResource cluster, ContainerResource setup)
    {
        cluster.SetupResources.Add(setup);

        foreach (var dependent in builder.Resources)
        {
            if (dependent is HelmReleaseResource release && ReferenceEquals(release.Parent, cluster)
                || dependent is K8sManifestResource manifest && ReferenceEquals(manifest.Parent, cluster))
            {
                WaitForClusterSetup(dependent, cluster);
            }
        }
    }

    private static IResourceBuilder<ContainerResource> EnsureDnsResource(IResourceBuilder<K3sClusterResource> builder)
    {
        var cluster = builder.Resource;

        if (cluster.DnsResource is { } existing)
            return builder.ApplicationBuilder.CreateResourceBuilder(existing);

        var containerKubeconfigFile = Path.Combine(cluster.KubeconfigDirectory!, "container", "kubeconfig.yaml");
        // Placeholder ensures Docker creates a file-level bind-mount, not a directory.
        K3sBuilderExtensions.EnsureKubeconfigPlaceholder(containerKubeconfigFile);

        // Needs getent, which alpine/kubectl has; a replacement image from WithKubectlImage must too.
        var (kubectlRegistry, kubectlImage, kubectlTag) = cluster.KubectlImageInfo;

        var dns = builder.ApplicationBuilder
            .AddContainer($"{cluster.Name}-dns", kubectlImage, kubectlTag)
            .WithImageRegistry(kubectlRegistry)
            .WithEntrypoint("/bin/sh")
            // Generated when the container starts, so every alias and customization is in it
            // whatever order they were declared in.
            .WithContainerFiles("/", (ctx, ct) =>
            {
                IEnumerable<ContainerFileSystemItem> items = [new ContainerFile
                {
                    Name = "coredns-setup.sh",
                    Contents = BuildCoreDnsScript(cluster),
                    Mode = K3sFileHelpers.ExecutableScriptMode,
                }];
                return Task.FromResult(items);
            })
            .WithContainerFiles(CustomizationsDirectory, (ctx, ct) =>
            {
                IEnumerable<ContainerFileSystemItem> items = [.. cluster.CoreDnsCustomizations
                    .Select(c => (ContainerFileSystemItem)new ContainerFile
                    {
                        Name = c.Key,
                        Contents = c.Value,
                    })];
                return Task.FromResult(items);
            })
            .WithArgs("/coredns-setup.sh")
            .WithBindMount(containerKubeconfigFile, K3sFileHelpers.ContainerKubeconfigPath, isReadOnly: true)
            .WithEnvironment("KUBECONFIG", K3sFileHelpers.ContainerKubeconfigPath)
            .WithParentRelationship(builder)
            .WaitFor(builder)
            .WithIconName("Globe")
            .ExcludeFromManifest();

        cluster.DnsResource = dns.Resource;
        AddClusterSetup(builder.ApplicationBuilder, cluster, dns.Resource);

        return dns;
    }

    // ── Script generation ─────────────────────────────────────────────────────

    // Visible for testing.
    internal static string BuildCoreDnsScript(K3sClusterResource cluster)
    {
        var sb = new StringBuilder("#!/bin/sh\nset -e\n");

        sb.AppendShellLine("_k3s_wait=0");
        sb.AppendShellLine($"until [ -f {K3sFileHelpers.ContainerKubeconfigPath} ] && kubectl cluster-info --kubeconfig {K3sFileHelpers.ContainerKubeconfigPath} > /dev/null 2>&1; do");
        sb.AppendShellLine("  _k3s_wait=$((_k3s_wait + 5))");
        sb.AppendShellLine("  if [ \"$_k3s_wait\" -ge 600 ]; then");
        sb.AppendShellLine("    echo 'Timed out waiting for k3s cluster to be ready after 10 minutes' >&2");
        sb.AppendShellLine("    exit 1");
        sb.AppendShellLine("  fi");
        sb.AppendShellLine("  echo 'Waiting for k3s cluster to be ready and reachable...'");
        sb.AppendShellLine("  sleep 5");
        sb.AppendShellLine("done");

        sb.AppendShellLine("rm -rf /tmp/coredns && mkdir -p /tmp/coredns");
        sb.AppendShellLine($"if [ -d {CustomizationsDirectory} ]; then cp {CustomizationsDirectory}/* /tmp/coredns/ 2>/dev/null || true; fi");

        if (cluster.HostAliases.Count > 0)
        {
            // Looked up here, on the Aspire network, where Docker's DNS answers — and every run,
            // because a recreated container can come back on another address.
            var aliases = cluster.HostAliases.ToList();
            for (var i = 0; i < aliases.Count; i++)
            {
                var (hostName, target) = aliases[i];
                sb.AppendShellLine($"_alias_{i}=$(getent hosts '{target.Name}' | awk '{{ print $1; exit }}')");
                sb.AppendShellLine($"if [ -z \"$_alias_{i}\" ]; then echo '{target.Name} does not resolve on the Aspire network' >&2; exit 1; fi");
                sb.AppendShellLine($"echo \"{hostName} -> $_alias_{i}\"");
            }

            // One server block answers every alias zone; nothing else is asked of it.
            sb.AppendShellLine("{");
            sb.AppendShellLine($"  echo '{string.Join(' ', aliases.Select(a => a.Key))} {{'");
            sb.AppendShellLine("  echo '    hosts {'");
            for (var i = 0; i < aliases.Count; i++)
                sb.AppendShellLine($"  echo \"        $_alias_{i} {aliases[i].Key}\"");
            sb.AppendShellLine("  echo '    }'");
            sb.AppendShellLine("  echo '}'");
            sb.AppendShellLine($"}} > /tmp/coredns/{HostAliasesFile}");
        }

        // From files, so no configuration has to survive being quoted into YAML.
        sb.AppendShellLine($"kubectl create configmap {CoreDnsConfigMap} --namespace kube-system --from-file=/tmp/coredns --dry-run=client -o yaml | kubectl apply -f -");

        // CoreDNS reads the custom files when it starts. k3s deploys it a little after its API
        // answers, so on a fresh cluster there is nothing to restart yet.
        sb.AppendShellLine("_coredns_wait=0");
        sb.AppendShellLine("until kubectl -n kube-system get deployment/coredns > /dev/null 2>&1; do");
        sb.AppendShellLine("  _coredns_wait=$((_coredns_wait + 2))");
        sb.AppendShellLine("  if [ \"$_coredns_wait\" -ge 300 ]; then");
        sb.AppendShellLine("    echo 'Timed out waiting for k3s to deploy CoreDNS after 5 minutes' >&2");
        sb.AppendShellLine("    exit 1");
        sb.AppendShellLine("  fi");
        sb.AppendShellLine("  sleep 2");
        sb.AppendShellLine("done");
        sb.AppendShellLine("kubectl -n kube-system rollout restart deployment/coredns");
        sb.AppendShellLine("kubectl -n kube-system rollout status deployment/coredns --timeout=5m");

        return sb.ToString();
    }

    [GeneratedRegex(@"^[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$")]
    private static partial Regex DnsName();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*\.(override|server)$")]
    private static partial Regex CoreDnsFileName();
}
