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
    /// Applies one or more Kubernetes YAML files — or a Kustomize overlay — to the cluster
    /// via a <c>rancher/kubectl</c> container. No host-side <c>kubectl</c> binary is required.
    /// <para>
    /// The container exits with code 0 after manifests are applied and any CRDs reach the
    /// <c>Established</c> condition. Use <c>WaitForCompletion(manifest)</c> on dependent resources.
    /// </para>
    /// <para>
    /// Three modes, selected automatically based on <paramref name="path"/>:
    /// <list type="bullet">
    ///   <item><b>Single file</b> — injected via <c>WithContainerFiles</c>, applied with <c>kubectl apply -f</c>.</item>
    ///   <item><b>Directory without <c>kustomization.yaml</c></b> — all <c>.yaml</c>/<c>.yml</c> files
    ///     injected via <c>WithContainerFiles</c>, applied with <c>kubectl apply -f</c>.</item>
    ///   <item><b>Kustomize overlay</b> (directory contains <c>kustomization.yaml</c> or
    ///     <c>kustomization.yml</c>) — directory bind-mounted (preserving relative base references),
    ///     applied with <c>kubectl apply -k</c>.</item>
    /// </list>
    /// </para>
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

        var absolutePath = System.IO.Path.IsPathRooted(path)
            ? path
            : System.IO.Path.GetFullPath(
                System.IO.Path.Combine(builder.ApplicationBuilder.AppHostDirectory, path));

        bool isDirectory = Directory.Exists(absolutePath);
        bool isKustomize = isDirectory && IsKustomizeDirectory(absolutePath);

        var manifest = new K8sManifestResource(name, absolutePath, cluster);
        cluster.AddManifest(manifest.Name);

        var containerKubeconfigDir = Path.Combine(cluster.KubeconfigDirectory!, "container");
        Directory.CreateDirectory(containerKubeconfigDir);

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
                    Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                         | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                         | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
                }];
                return Task.FromResult(items);
            })
            .WithArgs("/kubectl-apply.sh")
            .WithBindMount(containerKubeconfigDir, "/root/.kube");

        if (isKustomize)
        {
            // Bind-mount the overlay directory so kubectl kustomize can resolve relative
            // references to base manifests (e.g. ../../base). WithContainerFiles copies
            // files, not directory structure, so it would break cross-directory references.
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
            .WithEnvironment("KUBECONFIG", "/root/.kube/kubeconfig.yaml")
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
        sb.AppendLine("until [ -f /root/.kube/kubeconfig.yaml ] && kubectl cluster-info --kubeconfig /root/.kube/kubeconfig.yaml > /dev/null 2>&1; do");
        sb.AppendLine("  echo 'Waiting for k3s cluster to be ready and reachable...'");
        sb.AppendLine("  sleep 5");
        sb.AppendLine("done");

        // Auto-detect kustomize: if a kustomization file is present, use -k.
        // Otherwise use -f with server-side apply.
        sb.AppendLine("if [ -f /k8s-manifests/kustomization.yaml ] || [ -f /k8s-manifests/kustomization.yml ]; then");
        sb.AppendLine("  echo 'Detected kustomization — using kubectl apply -k'");
        sb.AppendLine("  kubectl apply -k /k8s-manifests --server-side --field-manager=aspire-k3s --force-conflicts");
        sb.AppendLine("else");
        sb.AppendLine("  kubectl apply -f /k8s-manifests --server-side --field-manager=aspire-k3s --force-conflicts");
        sb.AppendLine("fi");

        // Wait for CRD Established condition if any CRDs are present.
        sb.AppendLine("if kubectl get crd --no-headers 2>/dev/null | grep -q .; then");
        sb.AppendLine("  kubectl wait --for=condition=Established crd --all --timeout=300s");
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
