using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class K3sClusterComponentsTests
{
    private static List<object> ServerArgs(K3sClusterResource resource)
    {
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);
        return [.. ctx.Args];
    }

    private static K3sClusterResource Build(Action<IResourceBuilder<K3sClusterResource>> configure)
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        configure(appBuilder.AddK3sCluster("k8s"));

        var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        return Assert.Single(model.Resources.OfType<K3sClusterResource>());
    }

    [Fact]
    public void ServiceLbAndMetricsServerAreDisabledByDefault()
    {
        var args = ServerArgs(Build(_ => { }));

        Assert.Contains("--disable=servicelb", args);
        Assert.Contains("--disable=metrics-server", args);
    }

    [Fact]
    public void WithEnabledComponentRemovesADefaultDisable()
    {
        var args = ServerArgs(Build(c => c.WithEnabledComponent("servicelb").WithEnabledComponent("metrics-server")));

        Assert.DoesNotContain("--disable=servicelb", args);
        Assert.DoesNotContain("--disable=metrics-server", args);
    }

    [Fact]
    public void WithEnabledComponentUndoesWithDisabledComponent()
    {
        var args = ServerArgs(Build(c => c.WithDisabledComponent("traefik").WithEnabledComponent("traefik")));

        Assert.DoesNotContain("--disable=traefik", args);
    }

    [Fact]
    public void WithDisabledComponentDoesNotRepeatTheFlag()
    {
        var args = ServerArgs(Build(c => c.WithDisabledComponent("traefik").WithDisabledComponent("traefik")));

        Assert.Single(args, a => Equals(a, "--disable=traefik"));
    }

    [Fact]
    public void WithEnabledComponentShouldThrowWhenComponentIsWhitespace()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        Assert.Throws<ArgumentException>(() => cluster.WithEnabledComponent(" "));
    }

    [Fact]
    public void NodeNameIsTheContainerHostnameWithoutADataVolume()
    {
        var args = ServerArgs(Build(_ => { }));

        Assert.DoesNotContain(args, a => a is string s && s.StartsWith("--node-name=", StringComparison.Ordinal));
    }

    [Fact]
    public void WithDataVolumePinsTheNodeNameAndKeepsTheNodePassword()
    {
        // A recreated container gets a new hostname; without a fixed name and the node password it
        // would register as a second node and leave volumes bound to the first one pending.
        var resource = Build(c => c.WithDataVolume());

        Assert.Contains("--node-name=k8s", ServerArgs(resource));
        Assert.Single(
            resource.Annotations.OfType<ContainerMountAnnotation>(),
            m => m.Target == "/etc/rancher/node" && m.Type == ContainerMountType.Volume);
    }

    [Fact]
    public void WithDataVolumeNamesTheNodeVolumeAfterACustomName()
    {
        var resource = Build(c => c.WithDataVolume("my-k3s-data"));

        var mount = Assert.Single(
            resource.Annotations.OfType<ContainerMountAnnotation>(),
            m => m.Target == "/etc/rancher/node");
        Assert.Equal("my-k3s-data-node", mount.Source);
    }

    [Fact]
    public void WithDataVolumeCalledTwiceKeepsOneMountPerDirectory()
    {
        var resource = Build(c => c.WithDataVolume().WithDataVolume());

        Assert.Single(resource.Annotations.OfType<ContainerMountAnnotation>(), m => m.Target == "/var/lib/rancher/k3s");
        Assert.Single(resource.Annotations.OfType<ContainerMountAnnotation>(), m => m.Target == "/etc/rancher/node");
        Assert.Single(ServerArgs(resource), a => Equals(a, "--node-name=k8s"));
    }
}
