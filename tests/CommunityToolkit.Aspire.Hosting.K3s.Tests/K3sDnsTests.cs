using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class K3sDnsTests
{
    private static ContainerResource Dns(IDistributedApplicationBuilder appBuilder) =>
        Assert.Single(appBuilder.Resources.OfType<ContainerResource>(), r => r.Name == "k8s-dns");

    private static bool WaitsForCompletionOf(IResource dependent, IResource resource) =>
        dependent.Annotations.OfType<WaitAnnotation>()
            .Any(w => ReferenceEquals(w.Resource, resource) && w.WaitType == WaitType.WaitForCompletion);

    [Fact]
    public void NoDnsResourceWithoutAliasesOrCustomizations()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app");

        Assert.DoesNotContain(appBuilder.Resources, r => r.Name == "k8s-dns");
        Assert.DoesNotContain(
            appBuilder.Resources.OfType<HelmReleaseResource>().Single().Annotations.OfType<WaitAnnotation>(),
            w => w.WaitType == WaitType.WaitForCompletion);
    }

    [Fact]
    public void WithHostAliasWaitsForTheAliasedContainer()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var postgres = appBuilder.AddContainer("postgres", "postgres");
        appBuilder.AddK3sCluster("k8s").WithHostAlias(postgres);

        var dns = Dns(appBuilder);

        Assert.Contains(dns.Annotations.OfType<WaitAnnotation>(), w => ReferenceEquals(w.Resource, postgres.Resource));
        Assert.Contains(dns.Annotations.OfType<ResourceRelationshipAnnotation>(), r => r.Resource.Name == "k8s");
    }

    [Fact]
    public void ReleasesAndManifestsWaitForTheDnsWhicheverIsDeclaredFirst()
    {
        // Why: a pod that starts before CoreDNS knows an alias fails to resolve it, and nothing
        // retries a release that helm already reported as failed.
        var appBuilder = DistributedApplication.CreateBuilder();
        var postgres = appBuilder.AddContainer("postgres", "postgres");
        var cluster = appBuilder.AddK3sCluster("k8s");
        var before = cluster.AddHelmRelease("before", "oci://registry/app");
        cluster.WithHostAlias(postgres);
        var after = cluster.AddHelmRelease("after", "oci://registry/app");
        var manifest = cluster.AddK8sManifest("config", "./k8s/config.yaml");

        var dns = Dns(appBuilder);

        Assert.True(WaitsForCompletionOf(before.Resource, dns));
        Assert.True(WaitsForCompletionOf(after.Resource, dns));
        Assert.True(WaitsForCompletionOf(manifest.Resource, dns));
    }

    [Fact]
    public void TheScriptResolvesEachAliasAndWritesOneServerBlock()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var postgres = appBuilder.AddContainer("postgres", "postgres");
        var temporal = appBuilder.AddContainer("temporal", "temporal");
        var cluster = appBuilder.AddK3sCluster("k8s")
            .WithHostAlias(postgres)
            .WithHostAlias(temporal, "temporal.aspire.internal");

        var script = K3sDnsBuilderExtensions.BuildCoreDnsScript(cluster.Resource);

        Assert.Contains("_alias_0=$(getent hosts 'postgres' | awk '{ print $1; exit }')", script);
        Assert.Contains("_alias_1=$(getent hosts 'temporal' | awk '{ print $1; exit }')", script);
        Assert.Contains("echo 'postgres temporal.aspire.internal {'", script);
        Assert.Contains("echo \"        $_alias_1 temporal.aspire.internal\"", script);
        Assert.Contains("} > /tmp/coredns/aspire-host-aliases.server", script);
        Assert.Contains("kubectl create configmap coredns-custom --namespace kube-system --from-file=/tmp/coredns", script);
        Assert.Contains("kubectl -n kube-system rollout restart deployment/coredns", script);
        Assert.DoesNotContain('\r', script);
    }

    [Fact]
    public void ACustomizationAloneWritesNoAliasBlock()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s")
            .WithCoreDnsCustomization("app.override", "rewrite name example.localhost traefik.kube-system.svc.cluster.local");

        var script = K3sDnsBuilderExtensions.BuildCoreDnsScript(cluster.Resource);

        Assert.Equal("rewrite name example.localhost traefik.kube-system.svc.cluster.local", cluster.Resource.CoreDnsCustomizations["app.override"]);
        Assert.DoesNotContain("getent", script);
        Assert.Contains("cp /coredns-custom/* /tmp/coredns/", script);
    }

    [Theory]
    [InlineData("app.conf")]
    [InlineData("../app.override")]
    [InlineData("dir/app.server")]
    [InlineData("aspire-host-aliases.server")]
    public void WithCoreDnsCustomizationRefusesNamesCoreDnsWouldNotImport(string fileName)
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        Assert.Throws<ArgumentException>(() => cluster.WithCoreDnsCustomization(fileName, "log"));
    }

    [Theory]
    [InlineData("-postgres")]
    [InlineData("postgres_db")]
    [InlineData("post gres")]
    public void WithHostAliasRefusesANameThatIsNotADnsName(string hostName)
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var postgres = appBuilder.AddContainer("postgres", "postgres");
        var cluster = appBuilder.AddK3sCluster("k8s");

        Assert.Throws<ArgumentException>(() => cluster.WithHostAlias(postgres, hostName));
    }

    [Fact]
    public void WithHostAliasShouldThrowWhenResourceIsNull()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        Assert.Throws<ArgumentNullException>(() => cluster.WithHostAlias(null!));
    }
}
