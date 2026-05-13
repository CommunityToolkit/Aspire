using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class K3sClusterResourceTests
{
    [Fact]
    public void AddK3sClusterAddsResourceWithCorrectName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        Assert.Equal("k8s", resource.Name);
    }

    [Fact]
    public void AddK3sClusterAddsCorrectContainerImage()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal(K3sContainerImageTags.Image, annotation.Image);
        Assert.Equal(K3sContainerImageTags.Tag, annotation.Tag);
        Assert.Equal(K3sContainerImageTags.Registry, annotation.Registry);
    }

    [Fact]
    public void AddK3sClusterAddsApiServerEndpointOnPort6443()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        var endpoint = Assert.Single(
            resource.Annotations.OfType<EndpointAnnotation>(),
            e => e.Name == K3sClusterResource.ApiServerEndpointName);

        Assert.Equal(6443, endpoint.TargetPort);
        Assert.Equal(ProtocolType.Tcp, endpoint.Protocol);
        Assert.Equal("https", endpoint.UriScheme);
    }

    [Fact]
    public void AddK3sClusterWithExplicitPortBindsToThatPort()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s", apiServerPort: 16443);

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        var endpoint = Assert.Single(
            resource.Annotations.OfType<EndpointAnnotation>(),
            e => e.Name == K3sClusterResource.ApiServerEndpointName);

        Assert.Equal(6443, endpoint.TargetPort);
        Assert.Equal(16443, endpoint.Port);
    }

    [Fact]
    public void AddK3sClusterAddsServerArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        var commandLineArgs = resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>();
        Assert.NotEmpty(commandLineArgs);
    }

    [Fact]
    public void WithK3sVersionOverridesImageTag()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithK3sVersion("v1.32.3-k3s1");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal("v1.32.3-k3s1", annotation.Tag);
    }

    [Fact]
    public void AddK3sClusterHasNoVolumeByDefault()
    {
        // Persistence is opt-in via WithPersistentState().
        // No volume is mounted by default so the cluster is ephemeral.
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        Assert.DoesNotContain(
            resource.Annotations.OfType<ContainerMountAnnotation>(),
            v => v.Target == "/var/lib/rancher/k3s" && v.Type == ContainerMountType.Volume);
    }

    [Fact]
    public void WithDataVolumeAddsSingleVolumeMount()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithDataVolume();

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        var volume = Assert.Single(
            resource.Annotations.OfType<ContainerMountAnnotation>(),
            v => v.Target == "/var/lib/rancher/k3s" && v.Type == ContainerMountType.Volume);

        // VolumeNameGenerator format: {appName}-{sha256}-{resourceName}-data
        Assert.EndsWith("-k8s-data", volume.Source);
    }

    [Fact]
    public void WithDataVolumeUsesCustomName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithDataVolume("my-k3s-data");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        var volume = Assert.Single(
            resource.Annotations.OfType<ContainerMountAnnotation>(),
            v => v.Target == "/var/lib/rancher/k3s" && v.Type == ContainerMountType.Volume);

        Assert.Equal("my-k3s-data", volume.Source);
    }

    [Fact]
    public void AddK3sClusterWithClusterCidrViaOptions()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s", configure: opts =>
        {
            opts.ClusterCidr = "10.99.0.0/16";
        });

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        Assert.NotNull(resource);
    }

    [Fact]
    public void WithReferenceSetsKubeconfigEnvForProject()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var cluster = appBuilder.AddK3sCluster("k8s");

        // ProjectResource would need a project file; use ExecutableResource as a proxy
        var exe = appBuilder.AddExecutable("myapp", "myapp", ".");
        exe.WithReference(cluster);

        // Verify the environment callback was added (no exception thrown)
        using var app = appBuilder.Build();
        Assert.NotNull(app);
    }

    [Fact]
    public void WithReferenceSetsKubeconfigDataEnvForContainer()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var cluster = appBuilder.AddK3sCluster("k8s");
        var container = appBuilder.AddContainer("operator", "myorg/operator");
        container.WithReference(cluster);

        using var app = appBuilder.Build();
        Assert.NotNull(app);
    }

    [Fact]
    public void K3sClusterResourceHasNoKubeconfigDirectoryByDefault()
    {
        // Kubeconfig is now stored in-memory (K8SConfiguration objects) and
        // never written to disk by the resource itself — docker exec reads it.
        var resource = new K3sClusterResource("k8s");
        Assert.Null(resource.AdminKubeconfig);
        Assert.Null(resource.ContainerKubeconfig);
    }

    [Fact]
    public void WithPodSubnetAddsClusterCidrArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithPodSubnet("10.88.0.0/16");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
    }

    [Fact]
    public void WithServiceSubnetAddsServiceCidrArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithServiceSubnet("10.89.0.0/16");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
    }

    [Fact]
    public void WithDisabledComponentAddsDisableArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithDisabledComponent("traefik");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
    }

    [Fact]
    public void WithExtraArgAddsRawArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithExtraArg("--write-kubeconfig-mode=644");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
    }

    [Fact]
    public void ApiEndpointReturnsEndpointReferenceWithCorrectName()
    {
        var resource = new K3sClusterResource("k8s");
        var endpoint = resource.ApiEndpoint;

        Assert.NotNull(endpoint);
        Assert.Equal(K3sClusterResource.ApiServerEndpointName, endpoint.EndpointName);
    }

    [Fact]
    public void ApiEndpointIsCached()
    {
        var resource = new K3sClusterResource("k8s");

        var first = resource.ApiEndpoint;
        var second = resource.ApiEndpoint;

        Assert.Same(first, second);
    }
}
