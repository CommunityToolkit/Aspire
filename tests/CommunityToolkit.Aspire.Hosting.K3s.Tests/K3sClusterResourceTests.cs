using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
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
    public void AddK3sClusterWithAgentCountParam()
    {
        // agentCount is now a direct nullable parameter on AddK3sCluster,
        // equivalent to calling WithAgentCount() on the returned builder.
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s", agentCount: 2);

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K3sClusterResource>());
        Assert.Equal(2, resource.AgentCount);
        Assert.Equal(2, appModel.Resources.OfType<K3sAgentResource>().Count());
    }

    [Fact]
    public void WithReferenceAddsResourceRelationshipAnnotationForProject()
    {
        // K3sClusterResource implements IResourceWithConnectionString so the standard
        // Aspire WithReference overload is used — it adds a ResourceRelationshipAnnotation.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var exe = appBuilder.AddExecutable("myapp", "myapp", ".");

        exe.WithReference(cluster);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var exeResource = Assert.Single(model.Resources.OfType<ExecutableResource>());

        // Standard WithReference adds a ResourceRelationshipAnnotation pointing to the cluster.
        Assert.Contains(
            exeResource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            a => ReferenceEquals(a.Resource, cluster.Resource));
    }

    [Fact]
    public void WithReferenceSetsKubeconfigEnvForProject()
    {
        // Standard WithReference(K3sClusterResource) uses IResourceWithConnectionString to inject
        // KUBECONFIG for host processes. Verify the env callback annotation is present.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var exe = appBuilder.AddExecutable("myapp", "myapp", ".");
        exe.WithReference(cluster);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var exeResource = Assert.Single(model.Resources.OfType<ExecutableResource>());

        Assert.Contains(
            exeResource.Annotations.OfType<EnvironmentCallbackAnnotation>(),
            a => a.Callback is not null);
    }

    [Fact]
    public void WithReferenceMountsKubeconfigFileForContainer()
    {
        // ApplyKubeconfigContainerOverride is what BeforeStartEvent calls for containers that
        // have a ResourceRelationshipAnnotation pointing to the cluster.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        appBuilder.AddContainer("operator", "myorg/operator");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var clusterResource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var containerResource = model.Resources.OfType<ContainerResource>().Single(r => r.Name == "operator");

        K3sBuilderExtensions.ApplyKubeconfigContainerOverride(containerResource, clusterResource);

        var mount = containerResource.Annotations
            .OfType<ContainerMountAnnotation>()
            .FirstOrDefault(m => m.Target == "/tmp/k3s-kubeconfig.yaml");

        Assert.NotNull(mount);
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
        Assert.True(mount.IsReadOnly);
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
        // ApplyKubeconfigContainerOverride is idempotent — the second call skips the
        // mount because the target path is already mounted.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var container = appBuilder.AddContainer("app", "myorg/app");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var clusterResource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var containerResource = model.Resources.OfType<ContainerResource>().Single(r => r.Name == "app");

        // Simulate two WithReference calls via two direct invocations of the override.
        K3sBuilderExtensions.ApplyKubeconfigContainerOverride(containerResource, clusterResource);
        K3sBuilderExtensions.ApplyKubeconfigContainerOverride(containerResource, clusterResource);

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
        // Image tag must propagate immediately — DCP uses ContainerImageAnnotation to
        // compute container identity before BeforeStartEvent fires.
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder
            .AddK3sCluster("k8s", agentCount: 2)
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
        // Standard Aspire WithLifetime<ContainerResource> sets the annotation immediately
        // on the cluster. Agent propagation happens in BeforeStartEvent.
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

    // ── IResourceWithConnectionString (K3sClusterResource) ───────────────────

    [Fact]
    public void ConnectionStringEnvironmentVariableIsKUBECONFIG()
    {
        var resource = new K3sClusterResource("k8s");
        Assert.Equal("KUBECONFIG", resource.ConnectionStringEnvironmentVariable);
    }

    [Fact]
    public async Task GetConnectionStringAsyncReturnsNullWhenDirectoryNotSet()
    {
        var resource = new K3sClusterResource("k8s") { KubeconfigDirectory = null };
        var result = await resource.GetConnectionStringAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task GetConnectionStringAsyncReturnsLocalKubeconfigPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"k8s-{Guid.NewGuid():N}");
        var resource = new K3sClusterResource("k8s") { KubeconfigDirectory = dir };

        var result = await resource.GetConnectionStringAsync();

        Assert.Equal(Path.Combine(dir, "local", "kubeconfig.yaml"), result);
    }

    // ── AddK3sCluster with agentCount param ──────────────────────────────────

    [Fact]
    public void AddK3sClusterWithAgentCountCreatesAgents()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s", agentCount: 2);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Equal(2, model.Resources.OfType<K3sAgentResource>().Count());
    }

    // ── WithHelmImage / WithKubectlImage ──────────────────────────────────────

    [Fact]
    public void WithHelmImageSetsTagOnly()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").WithHelmImage(tag: "3.18.0");

        using var app = appBuilder.Build();
        var resource = Assert.Single(app.Services
            .GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<K3sClusterResource>());

        var (_, _, tag) = resource.HelmImageInfo;
        Assert.Equal("3.18.0", tag);
    }

    [Fact]
    public void WithHelmImageSetsAllComponents()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s")
            .WithHelmImage(tag: "3.18.0", image: "my/helm", registry: "my.registry.io");

        using var app = appBuilder.Build();
        var resource = Assert.Single(app.Services
            .GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<K3sClusterResource>());

        var (registry, image, tag) = resource.HelmImageInfo;
        Assert.Equal("my.registry.io", registry);
        Assert.Equal("my/helm", image);
        Assert.Equal("3.18.0", tag);
    }

    [Fact]
    public void WithHelmImageNullParametersPreserveCurrentValues()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var (origRegistry, origImage, _) = cluster.Resource.HelmImageInfo;

        cluster.WithHelmImage(tag: "3.18.0"); // only tag changed

        var (registry, image, tag) = cluster.Resource.HelmImageInfo;
        Assert.Equal(origRegistry, registry);
        Assert.Equal(origImage, image);
        Assert.Equal("3.18.0", tag);
    }

    [Fact]
    public void WithHelmImageShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.WithHelmImage("3.18.0"));
    }

    [Fact]
    public void WithKubectlImageSetsTagOnly()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").WithKubectlImage(tag: "1.37.0");

        using var app = appBuilder.Build();
        var resource = Assert.Single(app.Services
            .GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<K3sClusterResource>());

        var (_, _, tag) = resource.KubectlImageInfo;
        Assert.Equal("1.37.0", tag);
    }

    [Fact]
    public void WithKubectlImageSetsAllComponents()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s")
            .WithKubectlImage(tag: "1.37.0", image: "my/kubectl", registry: "my.registry.io");

        using var app = appBuilder.Build();
        var resource = Assert.Single(app.Services
            .GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<K3sClusterResource>());

        var (registry, image, tag) = resource.KubectlImageInfo;
        Assert.Equal("my.registry.io", registry);
        Assert.Equal("my/kubectl", image);
        Assert.Equal("1.37.0", tag);
    }

    [Fact]
    public void WithKubectlImageShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.WithKubectlImage("1.37.0"));
    }

    // ── Options configure callback ────────────────────────────────────────────

    [Fact]
    public void AddK3sClusterWithServiceCidrViaOptions()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithServiceSubnet("10.99.0.0/16");

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
    public void AddK3sClusterWithExtraArgsViaOptions()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddK3sCluster("k8s").WithExtraArg("--write-kubeconfig-mode=644");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K3sClusterResource>());
        var ctx = new CommandLineArgsCallbackContext([]);
        foreach (var a in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            a.Callback(ctx);

        Assert.Contains("--write-kubeconfig-mode=644", ctx.Args);
    }
}
