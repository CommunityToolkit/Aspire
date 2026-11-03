using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class K3sCertificateTests
{
    private static ContainerResource Certificate(IDistributedApplicationBuilder appBuilder) =>
        Assert.Single(appBuilder.Resources.OfType<ContainerResource>(), r => r.Name == "k8s-certificate");

    [Fact]
    public void WithIngressDeveloperCertificateAddsOneCertificateResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").WithIngressDeveloperCertificate().WithIngressDeveloperCertificate();

        var certificate = Certificate(appBuilder);

        Assert.Contains(certificate.Annotations.OfType<ResourceRelationshipAnnotation>(), r => r.Resource.Name == "k8s");
        Assert.Contains(certificate.Annotations.OfType<WaitAnnotation>(), w => w.Resource.Name == "k8s");
    }

    [Fact]
    public void ReleasesWaitForTheCertificateWhicheverIsDeclaredFirst()
    {
        // Why: a release that starts before its namespace has the Secret serves Traefik's own
        // certificate and fails to mount the trusted one.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var before = cluster.AddHelmRelease("before", "oci://registry/app");
        cluster.WithIngressDeveloperCertificate();
        var after = cluster.AddHelmRelease("after", "oci://registry/app");

        var certificate = Certificate(appBuilder);

        Assert.Contains(before.Resource.Annotations.OfType<WaitAnnotation>(),
            w => ReferenceEquals(w.Resource, certificate) && w.WaitType == WaitType.WaitForCompletion);
        Assert.Contains(after.Resource.Annotations.OfType<WaitAnnotation>(),
            w => ReferenceEquals(w.Resource, certificate) && w.WaitType == WaitType.WaitForCompletion);
    }

    [Fact]
    public void TheScriptStoresTheSecretInKubeSystemAndEveryReleaseNamespace()
    {
        var cluster = new K3sClusterResource("k8s");

        var script = K3sCertificateBuilderExtensions.BuildCertificateScript(cluster, ["lembos", "observability", "lembos"]);

        Assert.Contains("kubectl -n 'kube-system' create secret tls aspire-developer-certificate", script);
        Assert.Contains("kubectl create namespace 'lembos' --dry-run=client -o yaml | kubectl apply -f -", script);
        Assert.Contains("kubectl -n 'observability' create secret tls aspire-developer-certificate", script);
        Assert.Single(script.Split('\n'), l => l.StartsWith("kubectl -n 'lembos' create secret", StringComparison.Ordinal));
        Assert.Contains("--cert=\"$ASPIRE_DEVELOPER_CERTIFICATE_FILE\" --key=\"$ASPIRE_DEVELOPER_CERTIFICATE_KEY_FILE\"", script);
        Assert.DoesNotContain('\r', script);
    }

    [Fact]
    public void TheScriptMakesTheCertificateTraefiksDefault()
    {
        var cluster = new K3sClusterResource("k8s");

        var script = K3sCertificateBuilderExtensions.BuildCertificateScript(cluster, []);

        Assert.Contains("until kubectl get crd tlsstores.traefik.io", script);
        Assert.Contains("kind: TLSStore", script);
        Assert.Contains("    secretName: aspire-developer-certificate", script);
    }

    [Fact]
    public void WithoutTraefikTheScriptWritesNoTlsStore()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s").WithDisabledComponent("traefik");

        var script = K3sCertificateBuilderExtensions.BuildCertificateScript(cluster.Resource, []);

        Assert.DoesNotContain("TLSStore", script);
        Assert.Contains("create secret tls aspire-developer-certificate", script);
    }

    [Fact]
    public void WithIngressDeveloperCertificateShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithIngressDeveloperCertificate());
    }
}
