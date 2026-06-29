using System.Text;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for applying Kubernetes YAML manifests to a k3s cluster.
/// </summary>
public static class K3sManifestBuilderExtensions
{
    /// <summary>
    /// Applies Kubernetes YAML manifests or a Kustomize overlay to the cluster.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="name">The Aspire resource name for this manifest application.</param>
    /// <param name="path">
    /// Path to a single <c>.yaml</c>/<c>.yml</c> file, a directory of YAML files, or a
    /// Kustomize overlay directory (containing <c>kustomization.yaml</c>). Relative paths
    /// are resolved against the AppHost project directory.
    /// </param>
    /// <returns>A builder for the <see cref="K8sManifestResource"/>.</returns>
    /// <remarks>
    /// <para>
    /// The apply mode is detected automatically from <paramref name="path"/>:
    /// <list type="bullet">
    ///   <item><b>Single file</b> — applied with <c>kubectl apply -f</c>.</item>
    ///   <item><b>Directory</b> (no <c>kustomization.yaml</c>) — all <c>.yaml</c>/<c>.yml</c>
    ///     files in the directory are applied with <c>kubectl apply -f</c>.</item>
    ///   <item><b>Kustomize overlay</b> (directory contains <c>kustomization.yaml</c> or
    ///     <c>kustomization.yml</c>) — applied with <c>kubectl apply -k</c>. The directory
    ///     is bind-mounted so that relative references to base manifests are preserved.</item>
    /// </list>
    /// </para>
    /// <para>
    /// The container exits with code 0 after all manifests are applied and any CRDs have
    /// reached the <c>Established</c> condition. No host-side <c>kubectl</c> binary is required.
    /// Use <c>WaitForCompletion(manifest)</c> on resources that must start only after the
    /// manifests are applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="name"/>, or <paramref name="path"/> is
    /// <see langword="null"/>.
    /// </exception>
    [AspireExport]
    public static IResourceBuilder<K8sManifestResource> AddK8sManifest(
        this IResourceBuilder<K3sClusterResource> builder,
        [ResourceName] string name,
        string path)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(path);

        var cluster = builder.Resource;

        var absolutePath = System.IO.Path.IsPathRooted(path)
            ? path
            : System.IO.Path.GetFullPath(
                System.IO.Path.Combine(builder.ApplicationBuilder.AppHostDirectory, path));

        bool isDirectory = Directory.Exists(absolutePath);
        bool isKustomize = isDirectory && IsKustomizeDirectory(absolutePath);

        var manifest = new K8sManifestResource(name, absolutePath, cluster);
        cluster.AddManifest(manifest.Name);

        var containerKubeconfigFile = Path.Combine(cluster.KubeconfigDirectory!, "container", "kubeconfig.yaml");
        // Placeholder ensures Docker creates a file-level bind-mount, not a directory.
        K3sBuilderExtensions.EnsureKubeconfigPlaceholder(containerKubeconfigFile);

        var (kubectlRegistry, kubectlImage, kubectlTag) = cluster.KubectlImageInfo;

        var resourceBuilder = builder.ApplicationBuilder
            .AddResource(manifest)
            .WithImage(kubectlImage, kubectlTag)
            .WithImageRegistry(kubectlRegistry)
            .WithEntrypoint("/bin/sh")
            // Script is injected at "/kubectl-apply.sh". The script auto-detects whether
            // /k8s-manifests contains a kustomization.yaml and uses -k or -f accordingly.
            .WithContainerFiles("/", (ctx, ct) =>
            {
                IEnumerable<ContainerFileSystemItem> items = [new ContainerFile
                {
                    Name = "kubectl-apply.sh",
                    Contents = BuildManifestScript(),
                    Mode = K3sFileHelpers.ExecutableScriptMode,
                }];
                return Task.FromResult(items);
            })
            .WithArgs("/kubectl-apply.sh")
            .WithBindMount(containerKubeconfigFile, K3sFileHelpers.ContainerKubeconfigPath, isReadOnly: true);

        if (isKustomize)
        {
            // Bind-mount the overlay directory so kubectl kustomize can resolve relative
            // references to base manifests (e.g. ../../base). WithContainerFiles copies
            // files, not directory structure, so it would break cross-directory references.
            // Read-write is intentional here: kustomize traverses into subdirectories.
            // The kubectl/helm scripts never write to /k8s-manifests, so this is latent.
            resourceBuilder.WithBindMount(absolutePath, "/k8s-manifests");
        }
        else
        {
            // Single file or regular directory — copy via async callback so the file(s)
            // need not exist when the AppHost is built (only when the container starts).
            // This mirrors WithContainerFiles(path, hostPath) semantics but without the
            // build-time path validation that Aspire's string overload performs.
            resourceBuilder.WithContainerFiles("/k8s-manifests", (ctx, ct) =>
            {
                IEnumerable<ContainerFileSystemItem> items;

                if (Directory.Exists(absolutePath))
                {
                    var files = Directory
                        .GetFiles(absolutePath, "*.yaml", SearchOption.TopDirectoryOnly)
                        .Concat(Directory.GetFiles(absolutePath, "*.yml", SearchOption.TopDirectoryOnly))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Order(StringComparer.OrdinalIgnoreCase);

                    items = [.. files.Select(f => new ContainerFile
                    {
                        Name = Path.GetFileName(f),
                        SourcePath = f,
                    })];
                }
                else
                {
                    items = [new ContainerFile
                    {
                        Name = Path.GetFileName(absolutePath),
                        SourcePath = absolutePath,
                    }];
                }

                return Task.FromResult(items);
            });
        }

        return resourceBuilder
            .WithEnvironment("KUBECONFIG", K3sFileHelpers.ContainerKubeconfigPath)
            .WithIconName("Code")
            .ExcludeFromManifest()
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = isKustomize ? "K8s Kustomize" : "K8s Manifest",
                State = KnownResourceStates.NotStarted,
                Properties =
                [
                    new ResourcePropertySnapshot("Path", absolutePath),
                    new ResourcePropertySnapshot("Mode", isKustomize ? "kustomize" : "apply"),
                ],
            });
    }

    // ── Script generation ─────────────────────────────────────────────────────

    internal static string BuildManifestScript()
    {
        var sb = new StringBuilder("#!/bin/sh\nset -e\n");

        // Poll until the kubeconfig exists AND the k3s API server is reachable.
        // DCP sets up container network aliases asynchronously, so the kubeconfig file
        // can appear in the bind-mount before the k8s hostname resolves in the kubectl
        // container. Using `kubectl cluster-info` verifies both the file and the network.
        sb.AppendLine("_k3s_wait=0");
        sb.AppendLine($"until [ -f {K3sFileHelpers.ContainerKubeconfigPath} ] && kubectl cluster-info --kubeconfig {K3sFileHelpers.ContainerKubeconfigPath} > /dev/null 2>&1; do");
        sb.AppendLine("  _k3s_wait=$((_k3s_wait + 5))");
        sb.AppendLine("  if [ \"$_k3s_wait\" -ge 600 ]; then");
        sb.AppendLine("    echo 'Timed out waiting for k3s cluster to be ready after 10 minutes' >&2");
        sb.AppendLine("    exit 1");
        sb.AppendLine("  fi");
        sb.AppendLine("  echo 'Waiting for k3s cluster to be ready and reachable...'");
        sb.AppendLine("  sleep 5");
        sb.AppendLine("done");

        // Auto-detect kustomize: if a kustomization file is present, use -k.
        // Otherwise use -f with server-side apply.
        // Capture output so we can extract any CRD names that were applied.
        sb.AppendLine("if [ -f /k8s-manifests/kustomization.yaml ] || [ -f /k8s-manifests/kustomization.yml ]; then");
        sb.AppendLine("  echo 'Detected kustomization — using kubectl apply -k'");
        sb.AppendLine("  APPLIED=$(kubectl apply -k /k8s-manifests --server-side --field-manager=aspire-k3s --force-conflicts)");
        sb.AppendLine("else");
        sb.AppendLine("  APPLIED=$(kubectl apply -f /k8s-manifests --server-side --field-manager=aspire-k3s --force-conflicts)");
        sb.AppendLine("fi");
        sb.AppendLine("echo \"$APPLIED\"");

        // Parse the apply output for CRD names — kubectl apply prints one line per resource
        // in the form "<kind>/<name> <verb>", e.g.:
        //   customresourcedefinition.apiextensions.k8s.io/widgets.example.com created
        // Only lines starting with "customresourcedefinition." belong to this apply.
        // This avoids touching pre-existing or concurrently installed cluster CRDs and
        // prevents busybox xargs from returning exit code 123 when grep finds no match.
        sb.AppendLine("CRD_NAMES=$(echo \"$APPLIED\" | grep '^customresourcedefinition\\.' | awk '{print $1}')");
        sb.AppendLine("if [ -n \"$CRD_NAMES\" ]; then");
        sb.AppendLine("  # shellcheck disable=SC2086");
        sb.AppendLine("  kubectl wait --for=condition=Established $CRD_NAMES --timeout=300s");
        sb.AppendLine("fi");

        return sb.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsKustomizeDirectory(string directory) =>
        File.Exists(System.IO.Path.Combine(directory, "kustomization.yaml")) ||
        File.Exists(System.IO.Path.Combine(directory, "kustomization.yml"));

    // Exposed for unit tests.
    internal static IReadOnlyList<string> ResolveFilesForTest(string path)
    {
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
            return [..Directory.GetFiles(dir, pattern).Order(StringComparer.OrdinalIgnoreCase)];

        return [path];
    }
}

#pragma warning restore ASPIREATS001
