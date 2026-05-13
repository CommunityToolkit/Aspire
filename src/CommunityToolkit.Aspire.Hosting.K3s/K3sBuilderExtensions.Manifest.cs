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
    /// Applies one or more Kubernetes YAML files to the cluster via
    /// <c>kubectl apply --server-side</c> running inside a <c>bitnami/kubectl</c> container.
    /// No host-side <c>kubectl</c> binary is required.
    /// <para>
    /// After applying the manifests the container waits for any CRDs to reach the
    /// <c>Established</c> condition, then exits with code 0. Use
    /// <c>WaitForCompletion(manifest)</c> on dependent resources.
    /// </para>
    /// <list type="bullet">
    ///   <item>A single file: <c>cluster.AddK8sManifest("crd", "./k8s/widget-crd.yaml")</c></item>
    ///   <item>A directory: all <c>.yaml</c>/<c>.yml</c> files applied (kubectl handles ordering).</item>
    /// </list>
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

        // Resolve to an absolute path so the bind-mount and container path are stable.
        var absolutePath = System.IO.Path.IsPathRooted(path)
            ? path
            : System.IO.Path.GetFullPath(
                System.IO.Path.Combine(builder.ApplicationBuilder.AppHostDirectory, path));

        string hostBindDir;
        string containerManifestPath;

        if (Directory.Exists(absolutePath))
        {
            hostBindDir = absolutePath;
            containerManifestPath = "/k8s-manifests";
        }
        else
        {
            hostBindDir = System.IO.Path.GetDirectoryName(absolutePath)!;
            containerManifestPath = $"/k8s-manifests/{System.IO.Path.GetFileName(absolutePath)}";
        }

        var manifest = new K8sManifestResource(name, absolutePath, cluster);
        cluster.AddManifest(manifest.Name);

        var containerKubeconfigDir = Path.Combine(cluster.KubeconfigDirectory!, "container");
        Directory.CreateDirectory(containerKubeconfigDir);

        var (kubectlRegistry, kubectlImage, kubectlTag) = cluster.KubectlImageInfo;

        return builder.ApplicationBuilder
            .AddResource(manifest)
            .WithImage(kubectlImage, kubectlTag)
            .WithImageRegistry(kubectlRegistry)
            .WithEntrypoint("/bin/sh")
            .WithContainerFiles("/", async (ctx, ct) =>
            {
                var script = BuildManifestScript(containerManifestPath);
                return [new ContainerFile
                {
                    Name = "kubectl-apply.sh",
                    Contents = script,
                    Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                         | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                         | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
                }];
            })
            .WithArgs("/kubectl-apply.sh")
            .WithBindMount(hostBindDir, "/k8s-manifests")
            .WithBindMount(containerKubeconfigDir, "/root/.kube")
            .WithEnvironment("KUBECONFIG", "/root/.kube/kubeconfig.yaml")
            .WithIconName("Code")
            .ExcludeFromManifest()
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "K8s Manifest",
                State = KnownResourceStates.NotStarted,
                Properties = [new ResourcePropertySnapshot("Path", absolutePath)],
            });
    }

    // ── Script generation ─────────────────────────────────────────────────────

    internal static string BuildManifestScript(string containerManifestPath)
    {
        var sb = new StringBuilder("#!/bin/sh\nset -e\n");

        // Poll until the k3s health check writes the kubeconfig — same pattern as the
        // helm installer. Replaces WaitFor(cluster) for child resources.
        sb.AppendLine("until [ -f /root/.kube/kubeconfig.yaml ]; do");
        sb.AppendLine("  echo 'Waiting for k3s cluster to be ready...'");
        sb.AppendLine("  sleep 5");
        sb.AppendLine("done");

        sb.AppendLine($"kubectl apply -f \"{containerManifestPath}\" --server-side --field-manager=aspire-k3s --force-conflicts");
        // Wait for CRD Established condition if any CRDs are present.
        // The check guard prevents failure when no CRDs were applied.
        sb.AppendLine("if kubectl get crd --no-headers 2>/dev/null | grep -q .; then");
        sb.AppendLine("  kubectl wait --for=condition=Established crd --all --timeout=300s");
        sb.AppendLine("fi");
        return sb.ToString();
    }

    // Keep for unit tests — file resolution logic is the same.
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
        {
            return [..Directory.GetFiles(dir, pattern).Order(StringComparer.OrdinalIgnoreCase)];
        }

        return [path];
    }
}

#pragma warning restore ASPIREATS001
