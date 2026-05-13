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
            .WithContainerFiles("/", async (ctx, ct) =>
            {
                var script = BuildHelmScript(release);
                return [new ContainerFile
                {
                    Name = "helm-install.sh",
                    Contents = script,
                    Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                         | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                         | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
                }];
            })
            .WithArgs("/helm-install.sh")
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
    /// Adds a Helm <c>--set key=value</c> argument to this release.
    /// </summary>
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

        // Poll until the k3s health check writes the kubeconfig — the file appears only
        // after all nodes are Ready. This replaces WaitFor(cluster) since a container
        // cannot WaitFor its IResourceWithParent.
        sb.AppendLine("until [ -f /root/.kube/kubeconfig.yaml ]; do");
        sb.AppendLine("  echo 'Waiting for k3s cluster to be ready...'");
        sb.AppendLine("  sleep 5");
        sb.AppendLine("done");

        if (release.RepoUrl is not null)
        {
            var alias = $"aspire-k3s-{release.ReleaseName}";
            sb.AppendLine($"helm repo add --force-update \"{alias}\" \"{release.RepoUrl}\"");
            sb.AppendLine($"helm repo update \"{alias}\"");
        }

        var chartRef = release.RepoUrl is not null
            ? $"aspire-k3s-{release.ReleaseName}/{release.Chart}"
            : release.Chart!;

        sb.Append($"helm upgrade --install \"{release.ReleaseName}\" \"{chartRef}\"");
        sb.Append($" --namespace \"{release.Namespace}\" --create-namespace");
        sb.Append(" --wait --timeout 10m");

        if (release.Version is not null)
            sb.Append($" --version \"{release.Version}\"");

        foreach (var (key, value) in release.HelmValues)
            sb.Append($" --set \"{key}={value}\"");

        return sb.ToString();
    }
}

#pragma warning restore ASPIREATS001
