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

        // ProjectResource would need a project file; use ExecutableResource as a proxy.
        var exe = appBuilder.AddExecutable("myapp", "myapp", ".");
        exe.WithReference(cluster);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var exeResource = Assert.Single(model.Resources.OfType<ExecutableResource>());

        // Executables receive KUBECONFIG pointing to local/kubeconfig.yaml on the host.
        Assert.Contains(
            exeResource.Annotations.OfType<EnvironmentCallbackAnnotation>(),
            a => a.Callback is not null);
    }

    [Fact]
    public void WithReferenceMountsKubeconfigFileForContainer()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var container = appBuilder.AddContainer("operator", "myorg/operator");
        container.WithReference(cluster);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = model.Resources
            .OfType<ContainerResource>()
            .Single(r => r.Name == "operator");

        // Containers receive a file-level bind-mount of container/kubeconfig.yaml at
        // /tmp/k3s-kubeconfig.yaml (not the directory). Mounting only the file prevents
        // kubectl's cache directories (cache/, http-cache/) from appearing on the host
        // and avoids concurrent-container cache corruption.
        var mount = containerResource.Annotations
            .OfType<ContainerMountAnnotation>()
            .FirstOrDefault(m => m.Target == "/tmp/k3s-kubeconfig.yaml");

        Assert.NotNull(mount);
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
        Assert.True(mount.IsReadOnly);
        // Source is the specific file, not the directory.
        Assert.EndsWith(Path.Combine(".k3s", "k8s", "container", "kubeconfig.yaml"), mount.Source);
    }

    [Fact]
    public void AddK3sClusterSetsKubeconfigDirectory()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var clusterBuilder = appBuilder.AddK3sCluster("k8s");

        // KubeconfigDirectory is set by AddK3sCluster under AppHostDirectory/.k3s/{name}/
        Assert.NotNull(clusterBuilder.Resource.KubeconfigDirectory);
        Assert.EndsWith(Path.Combine(".k3s", "k8s"), clusterBuilder.Resource.KubeconfigDirectory);
    }

    [Fact]
    public void WithPodSubnetAddsClusterCidrArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithPodSubnet("10.88.0.0/16");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        var args = resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>();
        Assert.Contains(args, a => a is not null); // arg callbacks registered
        // Verify the actual arg value by evaluating the callbacks.
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in args) a.Callback(ctx);
        Assert.Contains("--cluster-cidr=10.88.0.0/16", ctx.Args);
    }

    [Fact]
    public void WithServiceSubnetAddsServiceCidrArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithServiceSubnet("10.89.0.0/16");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);
        Assert.Contains("--service-cidr=10.89.0.0/16", ctx.Args);
    }

    [Fact]
    public void WithDisabledComponentAddsDisableArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithDisabledComponent("traefik");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);
        Assert.Contains("--disable=traefik", ctx.Args);
    }

    [Fact]
    public void WithExtraArgAddsRawArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithExtraArg("--write-kubeconfig-mode=644");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);
        Assert.Contains("--write-kubeconfig-mode=644", ctx.Args);
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

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public void WithDataVolumeCalledTwiceProducesOnlyOneVolumeMount()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithDataVolume().WithDataVolume();

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var mounts = resource.Annotations
            .OfType<ContainerMountAnnotation>()
            .Where(v => v.Target == "/var/lib/rancher/k3s" && v.Type == ContainerMountType.Volume)
            .ToList();

        // Idempotent: second call replaces the first rather than duplicating the mount.
        Assert.Single(mounts);
    }

    [Fact]
    public void WithReferenceContainerCalledTwiceProducesOnlyOneBindMount()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var container = appBuilder.AddContainer("app", "myorg/app");

        container.WithReference(cluster);
        container.WithReference(cluster);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = model.Resources
            .OfType<ContainerResource>()
            .Single(r => r.Name == "app");

        // File-level bind-mount at /tmp/k3s-kubeconfig.yaml must not be duplicated —
        // Docker rejects containers with duplicate mount targets.
        var kubeconfigMounts = containerResource.Annotations
            .OfType<ContainerMountAnnotation>()
            .Where(m => m.Target == "/tmp/k3s-kubeconfig.yaml")
            .ToList();

        Assert.Single(kubeconfigMounts);
    }

    // ── WithK3sVersion propagation ────────────────────────────────────────────

    [Fact]
    public void WithK3sVersionSyncsAllAgentImageTags()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder
            .AddK3sCluster("k8s", configure: opts => opts.AgentCount = 2)
            .WithK3sVersion("v1.30.0-k3s1");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var agents = model.Resources.OfType<K3sAgentResource>().ToList();
        Assert.Equal(2, agents.Count);

        foreach (var agent in agents)
        {
            var img = Assert.Single(agent.Annotations.OfType<ContainerImageAnnotation>());
            Assert.Equal("v1.30.0-k3s1", img.Tag);
        }
    }

    [Fact]
    public void WithK3sVersionCalledTwiceAppliesLastTag()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s")
            .WithK3sVersion("v1.29.0-k3s1")
            .WithK3sVersion("v1.30.0-k3s1");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("v1.30.0-k3s1", annotation.Tag);
    }

    // ── WithLifetime ──────────────────────────────────────────────────────────

    [Fact]
    public void WithLifetimePersistentSetsClusterLifetimeAnnotation()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithLifetime(ContainerLifetime.Persistent);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<ContainerLifetimeAnnotation>());
        Assert.Equal(ContainerLifetime.Persistent, annotation.Lifetime);
    }

    [Fact]
    public void WithLifetimeSessionSetsClusterLifetimeAnnotation()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithLifetime(ContainerLifetime.Session);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<ContainerLifetimeAnnotation>());
        Assert.Equal(ContainerLifetime.Session, annotation.Lifetime);
    }

    // ── Argument accumulation ─────────────────────────────────────────────────

    [Fact]
    public void WithDisabledComponentCalledMultipleTimesAccumulatesArgs()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s")
            .WithDisabledComponent("traefik")
            .WithDisabledComponent("coredns");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);

        Assert.Contains("--disable=traefik", ctx.Args);
        Assert.Contains("--disable=coredns", ctx.Args);
    }

    [Fact]
    public void WithExtraArgCalledMultipleTimesAccumulatesArgs()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s")
            .WithExtraArg("--write-kubeconfig-mode=644")
            .WithExtraArg("--node-label=env=dev");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);

        Assert.Contains("--write-kubeconfig-mode=644", ctx.Args);
        Assert.Contains("--node-label=env=dev", ctx.Args);
    }

    // ── Default args (absence checks) ────────────────────────────────────────

    [Fact]
    public void AddK3sClusterDefaultsToNoClusterCidrArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);

        Assert.DoesNotContain(ctx.Args, arg => arg is string s && s.StartsWith("--cluster-cidr="));
    }

    [Fact]
    public void AddK3sClusterDefaultsToNoServiceCidrArg()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);

        Assert.DoesNotContain(ctx.Args, arg => arg is string s && s.StartsWith("--service-cidr="));
    }

    // ── Options configure callback ────────────────────────────────────────────

    [Fact]
    public void AddK3sClusterWithServiceCidrViaOptions()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s", configure: opts =>
        {
            opts.ServiceCidr = "10.99.0.0/16";
        });

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);

        Assert.Contains("--service-cidr=10.99.0.0/16", ctx.Args);
    }

    [Fact]
    public void AddK3sClusterWithMultipleDisabledComponentsViaOptions()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s", configure: opts =>
        {
            opts.DisabledComponents.Add("traefik");
            opts.DisabledComponents.Add("coredns");
        });

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);

        Assert.Contains("--disable=traefik", ctx.Args);
        Assert.Contains("--disable=coredns", ctx.Args);
    }

    [Fact]
    public void AddK3sClusterWithExtraArgsViaOptions()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s", configure: opts =>
        {
            opts.ExtraArgs.Add("--write-kubeconfig-mode=644");
        });

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);

        Assert.Contains("--write-kubeconfig-mode=644", ctx.Args);
    }
}
