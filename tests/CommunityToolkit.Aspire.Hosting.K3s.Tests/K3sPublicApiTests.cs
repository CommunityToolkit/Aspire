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
}
