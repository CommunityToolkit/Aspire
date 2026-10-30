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
    /// The chart name, e.g. <c>argo-cd</c> for a repo chart, <c>oci://registry/chart</c>
    /// for an OCI reference, or the path of a chart directory — one containing
    /// <c>Chart.yaml</c> — relative to the AppHost project directory. Provide
    /// <paramref name="repo"/> when using a named repo chart.
    /// </param>
    /// <param name="repo">
    /// Optional Helm repository URL, e.g. <c>https://argoproj.github.io/argo-helm</c>.
    /// When provided, the repo is added and updated before installation.
    /// </param>
    /// <param name="version">
    /// Optional chart version to pin, e.g. <c>7.8.0</c>. When <see langword="null"/> the
    /// latest version available in the repository is installed. Not used with a chart
    /// directory, which carries its own version in <c>Chart.yaml</c>.
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
    /// Customize the release with <see cref="WithHelmValue(IResourceBuilder{HelmReleaseResource}, string, string)"/>
    /// for individual key/value pairs or <see cref="WithHelmValuesFile"/> to supply a full YAML
    /// values file.
    /// </para>
    /// <para>
    /// A chart directory is mounted read-only and copied inside the container, where its
    /// dependencies are fetched with <c>helm dependency build</c> — so the chart's
    /// <c>charts/</c> directory does not have to be built on the host. Every HTTP repository
    /// its dependencies come from is added first, because Helm resolves a dependency only from
    /// a repository it knows. A dependency on another chart directory (<c>file://</c>) is
    /// mounted beside it, at the same relative path, and its own dependencies are built first.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="name"/>, or <paramref name="chart"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="version"/> is given for a chart directory.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A <c>file://</c> dependency of the chart directory shares no directory with it, such as one
    /// on another drive.
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

        var localChartDirectory = repo is null
            ? ResolveLocalChartDirectory(builder.ApplicationBuilder.AppHostDirectory, chart)
            : null;

        if (localChartDirectory is not null && version is not null)
        {
            throw new ArgumentException(
                $"'{chart}' is a chart directory, which carries its own version in Chart.yaml. Pass a version only for a repository or OCI chart.",
                nameof(version));
        }

        var release = new HelmReleaseResource(name, releaseName: name, @namespace, cluster)
        {
            Chart = chart,
            RepoUrl = repo,
            Version = version,
            LocalChart = localChartDirectory is not null ? HelmLocalChart.Load(localChartDirectory) : null,
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

        var releaseBuilder = builder.ApplicationBuilder
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
            // Values resolved at start, read when the container starts rather than added by each
            // WithHelmValue call, so a key set again or moved back to a literal value leaves no
            // variable behind.
            .WithEnvironment(context =>
            {
                foreach (var variable in release.HelmValueVariables.Values)
                    context.EnvironmentVariables[variable.Name] = variable.Value;
            })
            .WithIconName("Rocket")
            .ExcludeFromManifest()
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Helm Release",
                State = KnownResourceStates.NotStarted,
                Properties =
                [
                    new ResourcePropertySnapshot("ReleaseName", name),
                    new ResourcePropertySnapshot("Chart", localChartDirectory ?? chart),
                    new ResourcePropertySnapshot("Namespace", @namespace),
                    new ResourcePropertySnapshot("Version", localChartDirectory is not null ? "local" : version ?? "latest"),
                ],
            });

        // Read-only: the script copies the charts before building their dependencies, so nothing
        // is written into the source tree.
        foreach (var (source, target) in release.LocalChart?.Mounts ?? [])
            releaseBuilder.WithBindMount(source, target, isReadOnly: true);

        // A workload must not start before the cluster's DNS knows its aliases, nor before the
        // certificate it serves or trusts is there.
        K3sDnsBuilderExtensions.WaitForClusterSetup(release, cluster);

        return releaseBuilder;
    }

    /// <summary>
    /// The absolute path of <paramref name="chart"/> when it names a chart directory, or
    /// <see langword="null"/> when it names a repository or OCI chart.
    /// </summary>
    private static string? ResolveLocalChartDirectory(string appHostDirectory, string chart)
    {
        if (chart.Contains("://", StringComparison.Ordinal))
            return null;

        var path = Path.IsPathRooted(chart)
            ? chart
            : Path.GetFullPath(Path.Combine(appHostDirectory, chart));

        return File.Exists(Path.Combine(path, "Chart.yaml")) ? path : null;
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
    /// braces, or backslashes that cannot be safely expressed with <see cref="WithHelmValue(IResourceBuilder{HelmReleaseResource}, string, string)"/>.
    /// Values files are applied before <c>--set</c> flags, so <see cref="WithHelmValue(IResourceBuilder{HelmReleaseResource}, string, string)"/>
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
    [AspireExportIgnore(Reason = "Covered by the withHelmValue export, whose value accepts a string, a parameter, an expression or an endpoint.")]
    public static IResourceBuilder<HelmReleaseResource> WithHelmValue(
        this IResourceBuilder<HelmReleaseResource> builder,
        string key,
        string value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        // Removing the key's variable also keeps its value out of the container's environment.
        builder.Resource.HelmValueVariables.Remove(key);
        builder.Resource.HelmValues[key] = value;
        return builder;
    }

    /// <summary>
    /// Adds a Helm <c>--set key=value</c> override whose value is resolved when the release
    /// runs — a connection string, a host name, a port.
    /// </summary>
    /// <param name="builder">The Helm release resource builder.</param>
    /// <param name="key">
    /// The Helm value path using dot notation, e.g. <c>database.host</c>.
    /// If the same key is set more than once, the last call wins.
    /// </param>
    /// <param name="value">The expression to resolve.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// The value reaches the Helm container as an environment variable and is escaped for
    /// <c>--set</c> inside it, so it never appears in the generated install script. Endpoint
    /// references resolve as a container on the Aspire network sees them; pods reach those
    /// addresses once the resource is made resolvable with
    /// <see cref="K3sDnsBuilderExtensions.WithHostAlias"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="key"/>, or <paramref name="value"/> is
    /// <see langword="null"/>.
    /// </exception>
    [AspireExportIgnore(Reason = "Covered by the withHelmValue export, whose value accepts a string, a parameter, an expression or an endpoint.")]
    public static IResourceBuilder<HelmReleaseResource> WithHelmValue(
        this IResourceBuilder<HelmReleaseResource> builder,
        string key,
        ReferenceExpression value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        return WithHelmValueVariable(builder, key, value);
    }

    /// <summary>
    /// Adds a Helm <c>--set key=value</c> override whose value is a parameter, such as a
    /// password.
    /// </summary>
    /// <param name="builder">The Helm release resource builder.</param>
    /// <param name="key">
    /// The Helm value path using dot notation, e.g. <c>auth.password</c>.
    /// If the same key is set more than once, the last call wins.
    /// </param>
    /// <param name="value">The parameter whose value is assigned.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// The value reaches the Helm container as an environment variable and is escaped for
    /// <c>--set</c> inside it, so a secret parameter never appears in the generated install
    /// script. Helm stores it in the release, as it stores every value it is given.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="key"/>, or <paramref name="value"/> is
    /// <see langword="null"/>.
    /// </exception>
    [AspireExportIgnore(Reason = "Covered by the withHelmValue export, whose value accepts a string, a parameter, an expression or an endpoint.")]
    public static IResourceBuilder<HelmReleaseResource> WithHelmValue(
        this IResourceBuilder<HelmReleaseResource> builder,
        string key,
        IResourceBuilder<ParameterResource> value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        return WithHelmValueVariable(builder, key, ReferenceExpression.Create($"{value.Resource}"));
    }

    /// <summary>
    /// Adds a Helm <c>--set key=value</c> override whose value is an endpoint's URL.
    /// </summary>
    /// <param name="builder">The Helm release resource builder.</param>
    /// <param name="key">
    /// The Helm value path using dot notation, e.g. <c>api.url</c>.
    /// If the same key is set more than once, the last call wins.
    /// </param>
    /// <param name="value">The endpoint whose URL is assigned.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// The URL is the one a container on the Aspire network uses. Pods reach it once the
    /// endpoint's resource is made resolvable with <see cref="K3sDnsBuilderExtensions.WithHostAlias"/>.
    /// For a single part of the endpoint, such as its port, pass an expression built from
    /// <c>EndpointReference.Property</c> instead.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="key"/>, or <paramref name="value"/> is
    /// <see langword="null"/>.
    /// </exception>
    [AspireExportIgnore(Reason = "Covered by the withHelmValue export, whose value accepts a string, a parameter, an expression or an endpoint.")]
    public static IResourceBuilder<HelmReleaseResource> WithHelmValue(
        this IResourceBuilder<HelmReleaseResource> builder,
        string key,
        EndpointReference value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        return WithHelmValueVariable(builder, key, ReferenceExpression.Create($"{value}"));
    }

    /// <summary>
    /// Adds a Helm <c>--set key=value</c> override to the release.
    /// </summary>
    /// <param name="builder">The Helm release resource builder.</param>
    /// <param name="key">
    /// The Helm value path using dot notation, e.g. <c>server.service.type</c>.
    /// If the same key is set more than once, the last call wins.
    /// </param>
    /// <param name="value">
    /// The value to assign: a string, a parameter, an expression, or an endpoint, resolved when
    /// the release runs.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Helm <c>--set</c> metacharacters (<c>,</c>, <c>{</c>, <c>}</c>, <c>\</c>) are escaped.
    /// Values resolved when the release runs reach the Helm container as environment variables,
    /// so a secret never appears in the generated install script.
    /// </remarks>
    [AspireExport("withHelmValue")]
    internal static IResourceBuilder<HelmReleaseResource> WithHelmValuePolyglot(
        this IResourceBuilder<HelmReleaseResource> builder,
        string key,
        [AspireUnion(
            typeof(string),
            typeof(ReferenceExpression),
            typeof(EndpointReference),
            typeof(IResourceBuilder<ParameterResource>))]
        object value) => value switch
        {
            string text => builder.WithHelmValue(key, text),
            ReferenceExpression expression => builder.WithHelmValue(key, expression),
            EndpointReference endpoint => builder.WithHelmValue(key, endpoint),
            IResourceBuilder<ParameterResource> parameter => builder.WithHelmValue(key, parameter),
            _ => throw new ArgumentException(
                $"A Helm value must be a string, a parameter, an expression or an endpoint, not {value?.GetType().Name ?? "null"}.",
                nameof(value)),
        };

    private static IResourceBuilder<HelmReleaseResource> WithHelmValueVariable(
        IResourceBuilder<HelmReleaseResource> builder,
        string key,
        ReferenceExpression value)
    {
        var release = builder.Resource;
        release.HelmValues.Remove(key);

        // One variable per key for the release's lifetime: setting the key again replaces the
        // value the variable carries.
        var name = release.HelmValueVariables.TryGetValue(key, out var existing)
            ? existing.Name
            : $"ASPIRE_HELM_VALUE_{release.HelmValueVariableCount++}";
        release.HelmValueVariables[key] = new HelmValueVariable(name, value);

        return builder.WithReferenceRelationship(value);
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
        sb.AppendShellLine("_k3s_wait=0");
        sb.AppendShellLine($"until [ -f {K3sFileHelpers.ContainerKubeconfigPath} ] && helm list --kubeconfig {K3sFileHelpers.ContainerKubeconfigPath} > /dev/null 2>&1; do");
        sb.AppendShellLine("  _k3s_wait=$((_k3s_wait + 5))");
        sb.AppendShellLine("  if [ \"$_k3s_wait\" -ge 600 ]; then");
        sb.AppendShellLine("    echo 'Timed out waiting for k3s cluster to be ready after 10 minutes' >&2");
        sb.AppendShellLine("    exit 1");
        sb.AppendShellLine("  fi");
        sb.AppendShellLine("  echo 'Waiting for k3s cluster to be ready and reachable...'");
        sb.AppendShellLine("  sleep 5");
        sb.AppendShellLine("done");

        if (release.RepoUrl is not null)
        {
            var alias = $"aspire-k3s-{release.ReleaseName}";
            sb.AppendShellLine($"helm repo add --force-update {ShellEscape(alias)} {ShellEscape(release.RepoUrl)}");
            sb.AppendShellLine($"helm repo update {ShellEscape(alias)}");
        }

        if (release.LocalChart is { } localChart)
        {
            // Copied out of the read-only mounts: helm dependency build writes charts/ beside
            // Chart.yaml.
            sb.AppendShellLine($"rm -rf {HelmLocalChart.WorkPath} && cp -R {HelmLocalChart.MountPath} {HelmLocalChart.WorkPath}");

            // helm dependency build resolves an HTTP repository only once it has been added. The
            // repositories were read from the charts on the host.
            var repositoryIndex = 0;
            foreach (var repository in localChart.Repositories)
                sb.AppendShellLine($"helm repo add --force-update {ShellEscape($"aspire-k3s-dependency-{repositoryIndex++}")} {ShellEscape(repository)} > /dev/null");

            // Helm refuses a chart whose dependencies are missing even when its values switch them
            // all off, so they are built every time; Helm itself decides whether a chart has any,
            // whatever form its Chart.yaml or requirements.yaml takes.
            foreach (var path in localChart.BuildPaths)
                sb.AppendShellLine($"helm dependency build {ShellEscape(path)}");
        }

        // Values resolved at start arrive as environment variables. Each is escaped for --set
        // here, inside the container, the way HelmEscape escapes literal values below. Command
        // substitution strips trailing newlines, so the value is followed by a '.' that is
        // removed afterwards.
        var resolvedValues = new List<(string Key, string ShellVariable)>();
        foreach (var (key, variable) in release.HelmValueVariables)
        {
            var shellVariable = $"_aspire_helm_value_{resolvedValues.Count}";
            sb.AppendShellLine($"{shellVariable}=$(printf '%s.' \"${variable.Name}\" | sed -e 's/[\\\\,{{}}]/\\\\&/g')");
            sb.AppendShellLine($"{shellVariable}=${{{shellVariable}%.}}");
            resolvedValues.Add((key, shellVariable));
        }

        var chartRef = release.LocalChart is not null
            ? release.LocalChart.ChartPath
            : release.RepoUrl is not null
                ? $"aspire-k3s-{release.ReleaseName}/{release.Chart}"
                : release.Chart!;

        sb.Append($"helm upgrade --install {ShellEscape(release.ReleaseName)} {ShellEscape(chartRef)}");
        sb.Append($" --namespace {ShellEscape(release.Namespace)} --create-namespace");
        sb.Append(" --wait --timeout 10m");

        if (release.Version is not null && release.LocalChart is null)
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

        // The key is quoted as a literal; the escaped value is expanded by the shell.
        foreach (var (key, shellVariable) in resolvedValues)
            sb.Append($" --set {ShellEscape($"{HelmEscape(key)}=")}\"${shellVariable}\"");

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
