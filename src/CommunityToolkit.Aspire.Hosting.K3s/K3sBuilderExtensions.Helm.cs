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
    /// Installs a Helm chart into the k3s cluster.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="name">
    /// The Aspire resource name; also used as the Helm release name passed to
    /// <c>helm upgrade --install</c>.
    /// </param>
    /// <param name="chart">
    /// The chart name, e.g. <c>argo-cd</c> for a repo chart or <c>oci://registry/chart</c>
    /// for an OCI reference. Provide <paramref name="repo"/> when using a named repo chart.
    /// </param>
    /// <param name="repo">
    /// Optional Helm repository URL, e.g. <c>https://argoproj.github.io/argo-helm</c>.
    /// When provided, the repo is added and updated before installation.
    /// </param>
    /// <param name="version">
    /// Optional chart version to pin, e.g. <c>7.8.0</c>. When <see langword="null"/> the
    /// latest version available in the repository is installed.
    /// </param>
    /// <param name="namespace">
    /// The Kubernetes namespace to install the release into. The namespace is created
    /// automatically if it does not exist. Defaults to <c>default</c>.
    /// </param>
    /// <returns>A builder for the <see cref="HelmReleaseResource"/>.</returns>
    /// <remarks>
    /// <para>
    /// The release runs as an <c>alpine/helm</c> container on the DCP network. No host-side
    /// <c>helm</c> binary is required. The container exits with code 0 when the release is
    /// fully installed and all workloads are ready. Use <c>WaitForCompletion(helmRelease)</c>
    /// on resources that must start only after the chart is ready.
    /// </para>
    /// <para>
    /// Customize the release with <see cref="WithHelmValue"/> for individual key/value pairs
    /// or <see cref="WithHelmValuesFile"/> to supply a full YAML values file.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="name"/>, or <paramref name="chart"/> is
    /// <see langword="null"/>.
    /// </exception>
    [AspireExport]
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
        // Ensure the host-side container/ directory exists so the health check can write to it.
        var containerKubeconfigFile = Path.Combine(cluster.KubeconfigDirectory!, "container", "kubeconfig.yaml");
        // Placeholder ensures Docker creates a file-level bind-mount, not a directory.
        K3sBuilderExtensions.EnsureKubeconfigPlaceholder(containerKubeconfigFile);

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
                    Mode = K3sFileHelpers.ExecutableScriptMode,
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
            // File-level mount: only the kubeconfig YAML is visible inside the container.
            // Mounting the full container/ directory would expose it to kubectl's cache
            // (cache/, http-cache/) and cause concurrent-container cache corruption.
            .WithBindMount(containerKubeconfigFile, K3sFileHelpers.ContainerKubeconfigPath, isReadOnly: true)
            .WithEnvironment("KUBECONFIG", K3sFileHelpers.ContainerKubeconfigPath)
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
    /// Supplies a YAML values file to the Helm release (<c>--values</c>).
    /// </summary>
    /// <param name="builder">The Helm release resource builder.</param>
    /// <param name="path">
    /// Path to the YAML values file on the host. Relative paths are resolved against the
    /// AppHost project directory. Call this method multiple times to supply additional files;
    /// they are applied in declaration order and later files win for duplicate keys.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Use this method for structured overrides — particularly values containing commas,
    /// braces, or backslashes that cannot be safely expressed with <see cref="WithHelmValue"/>.
    /// Values files are applied before <c>--set</c> flags, so <see cref="WithHelmValue"/>
    /// always takes precedence.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or whitespace.</exception>
    [AspireExport]
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
    /// Adds a Helm <c>--set key=value</c> override to the release.
    /// </summary>
    /// <param name="builder">The Helm release resource builder.</param>
    /// <param name="key">
    /// The Helm value path using dot notation, e.g. <c>server.service.type</c>.
    /// If the same key is set more than once, the last call wins.
    /// </param>
    /// <param name="value">The value to assign.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Helm <c>--set</c> metacharacters (<c>,</c>, <c>{</c>, <c>}</c>, <c>\</c>) in
    /// <paramref name="key"/> or <paramref name="value"/> are automatically escaped.
    /// For values that contain these characters in ways Helm cannot represent safely,
    /// use <see cref="WithHelmValuesFile"/> instead.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="key"/>, or <paramref name="value"/> is
    /// <see langword="null"/>.
    /// </exception>
    [AspireExport]
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
        sb.AppendLine("_k3s_wait=0");
        sb.AppendLine($"until [ -f {K3sFileHelpers.ContainerKubeconfigPath} ] && helm list --kubeconfig {K3sFileHelpers.ContainerKubeconfigPath} > /dev/null 2>&1; do");
        sb.AppendLine("  _k3s_wait=$((_k3s_wait + 5))");
        sb.AppendLine("  if [ \"$_k3s_wait\" -ge 600 ]; then");
        sb.AppendLine("    echo 'Timed out waiting for k3s cluster to be ready after 10 minutes' >&2");
        sb.AppendLine("    exit 1");
        sb.AppendLine("  fi");
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
        // Apply HelmEscape to BOTH key and value: Helm's --set parser splits on commas
        // and treats braces/brackets as list/map syntax in both positions.
        foreach (var (key, value) in release.HelmValues)
            sb.Append($" --set {ShellEscape($"{HelmEscape(key)}={HelmEscape(value)}")}");

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
