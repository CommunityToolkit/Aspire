using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class K3sPublicApiTests
{
    [Fact]
    public void AddK3sClusterShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        var action = () => builder.AddK3sCluster("k8s");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddK3sClusterShouldThrowWhenNameIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        string name = null!;

        var action = () => builder.AddK3sCluster(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void WithK3sVersionShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;

        var action = () => builder.WithK3sVersion("v1.32.3-k3s1");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithPodSubnetShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;

        var action = () => builder.WithPodSubnet("10.42.0.0/16");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithServiceSubnetShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;

        var action = () => builder.WithServiceSubnet("10.43.0.0/16");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithDisabledComponentShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;

        var action = () => builder.WithDisabledComponent("traefik");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithExtraArgShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;

        var action = () => builder.WithExtraArg("--write-kubeconfig-mode=644");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithReferenceShouldThrowWhenDestinationIsNull()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");
        IResourceBuilder<ContainerResource> destination = null!;

        var action = () => destination.WithReference(cluster);

        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void WithReferenceShouldThrowWhenSourceIsNull()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var container = appBuilder.AddContainer("app", "myimage");
        IResourceBuilder<K3sClusterResource> source = null!;

        var action = () => container.WithReference(source);

        Assert.Throws<ArgumentNullException>(action);
    }

    // ── WithK3sVersion argument guards ────────────────────────────────────────

    [Fact]
    public void WithK3sVersionShouldThrowWhenTagIsNull()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");
        string tag = null!;

        var action = () => cluster.WithK3sVersion(tag);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(tag), exception.ParamName);
    }

    [Fact]
    public void WithK3sVersionShouldThrowWhenTagIsWhitespace()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");

        var action = () => cluster.WithK3sVersion("   ");

        Assert.Throws<ArgumentException>(action);
    }

    // ── WithPodSubnet argument guards ─────────────────────────────────────────

    [Fact]
    public void WithPodSubnetShouldThrowWhenCidrIsNull()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");
        string cidr = null!;

        var action = () => cluster.WithPodSubnet(cidr);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(cidr), exception.ParamName);
    }

    [Fact]
    public void WithPodSubnetShouldThrowWhenCidrIsWhitespace()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");

        var action = () => cluster.WithPodSubnet("   ");

        Assert.Throws<ArgumentException>(action);
    }

    // ── WithServiceSubnet argument guards ─────────────────────────────────────

    [Fact]
    public void WithServiceSubnetShouldThrowWhenCidrIsNull()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");
        string cidr = null!;

        var action = () => cluster.WithServiceSubnet(cidr);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(cidr), exception.ParamName);
    }

    [Fact]
    public void WithServiceSubnetShouldThrowWhenCidrIsWhitespace()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");

        var action = () => cluster.WithServiceSubnet("   ");

        Assert.Throws<ArgumentException>(action);
    }

    // ── WithDisabledComponent argument guards ─────────────────────────────────

    [Fact]
    public void WithDisabledComponentShouldThrowWhenComponentIsNull()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");
        string component = null!;

        var action = () => cluster.WithDisabledComponent(component);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(component), exception.ParamName);
    }

    [Fact]
    public void WithDisabledComponentShouldThrowWhenComponentIsWhitespace()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");

        var action = () => cluster.WithDisabledComponent("   ");

        Assert.Throws<ArgumentException>(action);
    }

    // ── WithExtraArg argument guards ──────────────────────────────────────────

    [Fact]
    public void WithExtraArgShouldThrowWhenArgIsNull()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");
        string arg = null!;

        var action = () => cluster.WithExtraArg(arg);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(arg), exception.ParamName);
    }

    [Fact]
    public void WithExtraArgShouldThrowWhenArgIsWhitespace()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");

        var action = () => cluster.WithExtraArg("   ");

        Assert.Throws<ArgumentException>(action);
    }

    // ── AddServiceEndpoint argument guards ────────────────────────────────────

    [Fact]
    public void AddServiceEndpointShouldThrowWhenNameIsNull()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");

        var action = () => cluster.AddServiceEndpoint(null!, "svc", 80);

        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddServiceEndpointShouldThrowWhenServiceNameIsNull()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");

        var action = () => cluster.AddServiceEndpoint("ep", null!, 80);

        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddServiceEndpointShouldThrowWhenServiceNameIsWhitespace()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");

        var action = () => cluster.AddServiceEndpoint("ep", "   ", 80);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void AddServiceEndpointShouldThrowWhenNamespaceIsWhitespace()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");

        var action = () => cluster.AddServiceEndpoint("ep", "svc", 80, @namespace: "   ");

        Assert.Throws<ArgumentException>(action);
    }

    // ── WithReference (service endpoint) argument guards ─────────────────────

    [Fact]
    public void WithReferenceServiceEndpointShouldThrowWhenDestinationIsNull()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");
        var ep = cluster.AddServiceEndpoint("ep", "svc", 80);
        IResourceBuilder<ContainerResource> destination = null!;

        var action = () => destination.WithReference(ep);

        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void WithReferenceServiceEndpointShouldThrowWhenSourceIsNull()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var container = appBuilder.AddContainer("app", "myimage");
        IResourceBuilder<K3sServiceEndpointResource> source = null!;

        var action = () => container.WithReference(source);

        Assert.Throws<ArgumentNullException>(action);
    }

    // ── WithHelmValuesFile argument guards ────────────────────────────────────

    [Fact]
    public void WithHelmValuesFileShouldThrowWhenPathIsWhitespace()
    {
        var appBuilder = new DistributedApplicationBuilder([]);
        var cluster = appBuilder.AddK3sCluster("k8s");
        var release = cluster.AddHelmRelease("argocd", "argo-cd");

        var action = () => release.WithHelmValuesFile("   ");

        Assert.Throws<ArgumentException>(action);
    }
}
