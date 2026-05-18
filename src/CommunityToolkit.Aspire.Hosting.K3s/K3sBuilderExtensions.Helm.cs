using System.Text;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting;

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
    /// The release runs as a <c>bitnami/helm</c> container on the DCP network, executing
    /// <c>helm upgrade --install --wait</c> then exiting. No host-side <c>helm</c> binary
    /// is required. Use <c>WaitForCompletion(helmRelease)</c> on resources that depend on
    /// the release being fully installed.
    /// </para>
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="name">Resource name — also used as the Helm release name.</param>
    /// <param name="chart">Chart name. Add <paramref name="repo"/> for remote charts.</param>
    /// <param name="repo">Optional Helm repository URL.</param>
    /// <param name="version">Optional chart version.</param>
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

        cluster.AddHelmRelease(release.Name, release.ReleaseName);

        // The helm installer container mounts container/kubeconfig.yaml so it can reach
        // the k3s API via DCP DNS (https://{clusterName}:6443). The directory is created
        // by AddK3sCluster; the kubeconfig file is written by K3sReadinessHealthCheck on
        // first successful health check. WaitFor(cluster) guarantees the file exists.
        var containerKubeconfigDir = Path.Combine(cluster.KubeconfigDirectory!, "container");
        Directory.CreateDirectory(containerKubeconfigDir);

        var (helmRegistry, helmImage, helmTag) = cluster.HelmImageInfo;

        return builder.ApplicationBuilder
            .AddResource(release)
            .WithImage(helmImage, helmTag)
            .WithImageRegistry(helmRegistry)
            .WithEntrypoint("/bin/sh")

            // The install script is injected as /helm-install.sh via WithContainerFiles.
            // The callback fires when the container is being started (after WaitFor(cluster)
            // is satisfied), so all WithHelmValue() calls have been made by then.
            .WithContainerFiles("/", (ctx, ct) =>
            {
                var script = BuildHelmScript(release);
                IEnumerable<ContainerFileSystemItem> items = [new ContainerFile
                {
                    Name = "helm-install.sh",
                    Contents = script,
                    Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                         | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                         | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
                }];
                return Task.FromResult(items);
            })
            .WithArgs("/helm-install.sh")
            // Inject host-side values files declared via WithHelmValuesFile.
            // The callback fires at container-start time so all WithHelmValuesFile() calls
            // have been made and ValuesFiles is fully populated.
            .WithContainerFiles("/helm-values", (ctx, ct) =>
            {
                IEnumerable<ContainerFileSystemItem> items = [.. release.ValuesFiles
                    .Select((hostPath, i) => (ContainerFileSystemItem)new ContainerFile
                    {
                        Name = $"{i}-{Path.GetFileName(hostPath)}",
                        SourcePath = hostPath,
                    })];
                return Task.FromResult(items);
            })
            .WithBindMount(containerKubeconfigDir, "/root/.kube")
            .WithEnvironment("KUBECONFIG", "/root/.kube/kubeconfig.yaml")
            .WithIconName("Rocket")
            .ExcludeFromManifest()
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
    /// Injects a host-side YAML values file into the Helm installer container and
    /// passes it as <c>--values /helm-values/{filename}</c> to <c>helm upgrade --install</c>.
    /// Multiple files are applied in the order they are declared (last wins for overlapping keys).
    /// </summary>
    /// <param name="builder">The Helm release resource builder.</param>
    /// <param name="path">
    /// Path to the values YAML file on the host. Relative paths are resolved against
    /// <c>AppHostDirectory</c>.
    /// </param>
    [AspireExport("withHelmValuesFile", Description = "Injects a host-side YAML values file into the Helm installer container")]
    public static IResourceBuilder<HelmReleaseResource> WithHelmValuesFile(
        this IResourceBuilder<HelmReleaseResource> builder,
        string path)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var absolutePath = System.IO.Path.IsPathRooted(path)
            ? path
            : System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    builder.ApplicationBuilder.AppHostDirectory, path));

        builder.Resource.ValuesFiles.Add(absolutePath);
        return builder;
    }

    /// <summary>
    /// Adds a Helm <c>--set key=value</c> argument to this release.
    /// </summary>
    [AspireExport("withHelmValue", Description = "Adds a --set key=value argument to the Helm release")]
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

    // ── Script generation ─────────────────────────────────────────────────────

    // Visible for testing.
    internal static string BuildHelmScript(HelmReleaseResource release)
    {
        var sb = new StringBuilder("#!/bin/sh\nset -e\n");

        // Poll until the kubeconfig exists AND the k3s API server is reachable.
        // DCP sets up container network aliases asynchronously, so the kubeconfig file
        // can appear in the bind-mount before the k8s hostname resolves in the helm
        // container. Using `helm list` (which calls the k8s API) verifies both the
        // file and the network path before proceeding.
        sb.AppendLine("until [ -f /root/.kube/kubeconfig.yaml ] && helm list --kubeconfig /root/.kube/kubeconfig.yaml > /dev/null 2>&1; do");
        sb.AppendLine("  echo 'Waiting for k3s cluster to be ready and reachable...'");
        sb.AppendLine("  sleep 5");
        sb.AppendLine("done");

        if (release.RepoUrl is not null)
        {
            var alias = $"aspire-k3s-{release.ReleaseName}";
            sb.AppendLine($"helm repo add --force-update {ShellEscape(alias)} {ShellEscape(release.RepoUrl)}");
            sb.AppendLine($"helm repo update {ShellEscape(alias)}");
        }

        var chartRef = release.RepoUrl is not null
            ? $"aspire-k3s-{release.ReleaseName}/{release.Chart}"
            : release.Chart!;

        sb.Append($"helm upgrade --install {ShellEscape(release.ReleaseName)} {ShellEscape(chartRef)}");
        sb.Append($" --namespace {ShellEscape(release.Namespace)} --create-namespace");
        sb.Append(" --wait --timeout 10m");

        if (release.Version is not null)
            sb.Append($" --version {ShellEscape(release.Version)}");

        // Values files: injected as {index}-{filename} to guarantee uniqueness and order.
        // Applied first so --set flags below can override individual keys.
        // Paths are single-quoted so spaces or special characters in filenames are safe.
        for (var i = 0; i < release.ValuesFiles.Count; i++)
        {
            var filename = $"{i}-{System.IO.Path.GetFileName(release.ValuesFiles[i])}";
            sb.Append($" --values {ShellEscape($"/helm-values/{filename}")}");
        }

        // --set flags override everything above (highest Helm precedence).
        // Values are Helm-escaped then shell-escaped:
        //   1. HelmEscape: escapes Helm's --set parser metacharacters (`,`, `{`, `}`, `\`)
        //      so Helm treats them as literals rather than array/map/list syntax.
        //   2. ShellEscape: wraps in POSIX single quotes so the shell passes the value
        //      to Helm without any shell interpretation.
        // For values containing commas, braces, or backslashes that Helm --set cannot
        // represent safely (e.g. multi-line strings), use WithHelmValuesFile instead.
        foreach (var (key, value) in release.HelmValues)
            sb.Append($" --set {ShellEscape($"{key}={HelmEscape(value)}")}");

        return sb.ToString();
    }

    /// <summary>
    /// Escapes Helm <c>--set</c> value metacharacters so that Helm's own parser treats
    /// them as literals rather than as array/map/list syntax delimiters.
    /// </summary>
    private static string HelmEscape(string value) =>
        value.Replace("\\", "\\\\")  // backslash first to avoid double-escaping
             .Replace(",", "\\,")    // comma separates multiple assignments
             .Replace("{", "\\{")    // brace opens a map/list literal
             .Replace("}", "\\}");

    /// <summary>
    /// Wraps <paramref name="value"/> in POSIX single quotes so that all shell
    /// metacharacters are treated as literals. Embedded single quotes are escaped
    /// with the standard POSIX technique: <c>'</c> → <c>'\''</c>.
    /// </summary>
    private static string ShellEscape(string value) =>
        $"'{value.Replace("'", "'\\''")}'";
}

#pragma warning restore ASPIREATS001
