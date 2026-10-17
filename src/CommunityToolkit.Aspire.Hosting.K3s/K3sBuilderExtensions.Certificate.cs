using System.Text;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting;

#pragma warning disable ASPIREATS001 // AspireExport is experimental
#pragma warning disable ASPIRECERTIFICATES001 // WithHttpsDeveloperCertificate is experimental

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for serving the AppHost's developer certificate from the k3s cluster.
/// </summary>
public static class K3sCertificateBuilderExtensions
{
    /// <summary>
    /// The name of the TLS Secret that holds the developer certificate, in <c>kube-system</c> and in the
    /// namespace of every Helm release of the cluster.
    /// </summary>
    public const string DeveloperCertificateSecretName = "aspire-developer-certificate";

    private const string CertificateFileVariable = "ASPIRE_DEVELOPER_CERTIFICATE_FILE";
    private const string KeyFileVariable = "ASPIRE_DEVELOPER_CERTIFICATE_KEY_FILE";

    /// <summary>
    /// Serves the AppHost's developer certificate — the one the machine trusts — from the cluster's
    /// bundled Traefik ingress controller, and makes it available to the cluster's workloads.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The certificate is stored as the TLS Secret <c>aspire-developer-certificate</c>, in
    /// <c>kube-system</c> and in the namespace of every Helm release of the cluster, and is made
    /// Traefik's default certificate: every host Traefik serves without a certificate of its own is
    /// served with it, so a browser on the machine trusts what answers. The developer certificate
    /// carries <c>*.dev.localhost</c>, so publish hosts under that domain.
    /// </para>
    /// <para>
    /// In a release's namespace a chart can name the Secret for its ingress and mount its
    /// <c>tls.crt</c> as a trusted certificate — the certificate is self-signed, so workloads that call
    /// each other through the ingress trust it as it is. The Secret holds the private key: this is a
    /// local development convenience, and only applies in run mode.
    /// </para>
    /// <para>
    /// Every Helm release and manifest of the cluster waits for the certificate to be stored. The
    /// certificate is stored again on every start, so a renewed developer certificate reaches the
    /// cluster without recreating it.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    [AspireExport]
    public static IResourceBuilder<K3sClusterResource> WithIngressDeveloperCertificate(
        this IResourceBuilder<K3sClusterResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var cluster = builder.Resource;

        if (cluster.CertificateResource is not null)
            return builder;

        var containerKubeconfigFile = Path.Combine(cluster.KubeconfigDirectory!, "container", "kubeconfig.yaml");
        // Placeholder ensures Docker creates a file-level bind-mount, not a directory.
        K3sBuilderExtensions.EnsureKubeconfigPlaceholder(containerKubeconfigFile);

        var (kubectlRegistry, kubectlImage, kubectlTag) = cluster.KubectlImageInfo;
        var resources = builder.ApplicationBuilder.Resources;

        var certificate = builder.ApplicationBuilder
            .AddContainer($"{cluster.Name}-certificate", kubectlImage, kubectlTag)
            .WithImageRegistry(kubectlRegistry)
            .WithEntrypoint("/bin/sh")
            // Generated when the container starts, so the namespace of every release is in it whatever
            // order the releases were declared in.
            .WithContainerFiles("/", (ctx, ct) =>
            {
                var namespaces = resources
                    .OfType<HelmReleaseResource>()
                    .Where(r => ReferenceEquals(r.Parent, cluster))
                    .Select(r => r.Namespace);

                IEnumerable<ContainerFileSystemItem> items = [new ContainerFile
                {
                    Name = "certificate-setup.sh",
                    Contents = BuildCertificateScript(cluster, namespaces),
                    Mode = K3sFileHelpers.ExecutableScriptMode,
                }];
                return Task.FromResult(items);
            })
            .WithArgs("/certificate-setup.sh")
            // The developer certificate and its key, as PEM files inside the container.
            .WithHttpsDeveloperCertificate()
            .WithHttpsCertificateConfiguration(ctx =>
            {
                ctx.EnvironmentVariables[CertificateFileVariable] = ctx.CertificatePath;
                ctx.EnvironmentVariables[KeyFileVariable] = ctx.KeyPath;
                return Task.CompletedTask;
            })
            .WithBindMount(containerKubeconfigFile, K3sFileHelpers.ContainerKubeconfigPath, isReadOnly: true)
            .WithEnvironment("KUBECONFIG", K3sFileHelpers.ContainerKubeconfigPath)
            .WithParentRelationship(builder)
            .WaitFor(builder)
            .WithIconName("Certificate")
            .ExcludeFromManifest();

        cluster.CertificateResource = certificate.Resource;
        K3sDnsBuilderExtensions.AddClusterSetup(builder.ApplicationBuilder, cluster, certificate.Resource);

        return builder;
    }

    // ── Script generation ─────────────────────────────────────────────────────

    // Visible for testing.
    internal static string BuildCertificateScript(K3sClusterResource cluster, IEnumerable<string> releaseNamespaces)
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

        // kube-system for Traefik; every release namespace, created ahead of helm, for the charts.
        var namespaces = new[] { "kube-system" }
            .Concat(releaseNamespaces.Where(n => n != "kube-system").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

        foreach (var ns in namespaces)
        {
            if (ns != "kube-system")
                sb.AppendShellLine($"kubectl create namespace '{ns}' --dry-run=client -o yaml | kubectl apply -f -");

            sb.AppendShellLine(
                $"kubectl -n '{ns}' create secret tls {DeveloperCertificateSecretName} --cert=\"${CertificateFileVariable}\" --key=\"${KeyFileVariable}\" --dry-run=client -o yaml | kubectl apply -f -");
        }

        if (!cluster.DisabledComponents.Contains("traefik", StringComparer.Ordinal))
        {
            // Traefik's CRDs arrive with its chart, a little after the API answers.
            sb.AppendShellLine("_traefik_wait=0");
            sb.AppendShellLine("until kubectl get crd tlsstores.traefik.io > /dev/null 2>&1; do");
            sb.AppendShellLine("  _traefik_wait=$((_traefik_wait + 2))");
            sb.AppendShellLine("  if [ \"$_traefik_wait\" -ge 300 ]; then");
            sb.AppendShellLine("    echo 'Timed out waiting for k3s to install the Traefik CRDs after 5 minutes' >&2");
            sb.AppendShellLine("    exit 1");
            sb.AppendShellLine("  fi");
            sb.AppendShellLine("  sleep 2");
            sb.AppendShellLine("done");

            // The default TLS store: what Traefik serves for a host that has no certificate of its own.
            sb.AppendShellLine("kubectl apply -f - <<'YAML'");
            sb.AppendShellLine("apiVersion: traefik.io/v1alpha1");
            sb.AppendShellLine("kind: TLSStore");
            sb.AppendShellLine("metadata:");
            sb.AppendShellLine("  name: default");
            sb.AppendShellLine("  namespace: kube-system");
            sb.AppendShellLine("spec:");
            sb.AppendShellLine("  defaultCertificate:");
            sb.AppendShellLine($"    secretName: {DeveloperCertificateSecretName}");
            sb.AppendShellLine("YAML");
        }

        return sb.ToString();
    }
}

#pragma warning restore ASPIRECERTIFICATES001
#pragma warning restore ASPIREATS001
