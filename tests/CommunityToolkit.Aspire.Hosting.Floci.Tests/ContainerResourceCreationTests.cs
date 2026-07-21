using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

public class ContainerResourceCreationTests
{
    [Fact]
    public void AddFlociBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddFloci("floci"));
    }

    [Fact]
    public void AddFlociBuilderNameShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddFloci(null!));
    }

    [Fact]
    public void AddFlociBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFloci("floci");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("floci", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(FlociContainerImageTags.Tag, imageAnnotations.Tag);
        Assert.Equal(FlociContainerImageTags.Image, imageAnnotations.Image);
        Assert.Equal(FlociContainerImageTags.Registry, imageAnnotations.Registry);
    }

    [Fact]
    public async Task AddFlociBuilderSetsEnvironmentVariables()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFloci("floci", defaultRegion: "eu-west-1", defaultAccountId: "111111111111");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<EnvironmentCallbackAnnotation>? envAnnotations));

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var annotation in envAnnotations!)
        {
            await annotation.Callback(context);
        }

        Assert.Equal("floci", envVars[FlociContainerResource.HostnameEnvVar].ToString());
        Assert.Equal("eu-west-1", envVars[FlociContainerResource.DefaultRegionEnvVar].ToString());
        Assert.Equal("111111111111", envVars[FlociContainerResource.DefaultAccountIdEnvVar].ToString());
        Assert.Equal("memory", envVars[FlociContainerResource.StorageModeEnvVar].ToString());
    }

    [Fact]
    public void AddFlociBuilderWithDataVolumeSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFloci("floci")
            .WithDataVolume("floci-data", isReadOnly: false);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotations));
        Assert.Equal(ContainerMountType.Volume, mountAnnotations.Type);
        Assert.Equal("/app/data", mountAnnotations.Target);
    }

    [Fact]
    public void WithFlociUIBuilderShouldNotBeNull()
    {
        IResourceBuilder<FlociContainerResource> builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.WithFlociUI());
    }

    [Fact]
    public void WithFlociUIAddsUIContainerResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFloci("floci")
            .WithFlociUI();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var flociResource = appModel.Resources.OfType<FlociContainerResource>().SingleOrDefault();
        var uiResource = appModel.Resources.OfType<FlociUIContainerResource>().SingleOrDefault();

        Assert.NotNull(flociResource);
        Assert.NotNull(uiResource);
        Assert.Equal("floci-ui", uiResource.Name);
        Assert.Same(flociResource, uiResource.Parent);

        Assert.True(uiResource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(FlociContainerImageTags.UITag, imageAnnotations.Tag);
        Assert.Equal(FlociContainerImageTags.UIImage, imageAnnotations.Image);
        Assert.Equal(FlociContainerImageTags.UIRegistry, imageAnnotations.Registry);

        Assert.True(uiResource.TryGetLastAnnotation(out EndpointAnnotation? endpointAnnotation));
        Assert.Equal(4500, endpointAnnotation.TargetPort);
        Assert.Equal("http", endpointAnnotation.Name);
    }

    [Fact]
    public async Task WithFlociUISetsEnvironmentVariables()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFloci("floci", defaultRegion: "eu-west-1", defaultAccountId: "111111111111")
            .WithFlociUI();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var uiResource = appModel.Resources.OfType<FlociUIContainerResource>().SingleOrDefault();

        Assert.NotNull(uiResource);
        Assert.True(uiResource.TryGetAnnotationsOfType(out IEnumerable<EnvironmentCallbackAnnotation>? envAnnotations));

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var annotation in envAnnotations!)
        {
            await annotation.Callback(context);
        }

        var endpointExpression = Assert.IsType<ReferenceExpression>(envVars[FlociUIContainerResource.EndpointEnvVar]);
        Assert.Contains("floci.bindings.aws.url", endpointExpression.ValueExpression);
        Assert.Equal("eu-west-1", envVars[FlociUIContainerResource.RegionEnvVar].ToString());
        Assert.Equal("test", envVars[FlociUIContainerResource.AccessKeyIdEnvVar].ToString());
        Assert.Equal("test", envVars[FlociUIContainerResource.SecretAccessKeyEnvVar].ToString());
        Assert.Equal("111111111111", envVars[FlociUIContainerResource.DefaultAccountIdEnvVar].ToString());
    }

    [Fact]
    public void WithFlociUICustomContainerNameSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFloci("floci")
            .WithFlociUI(containerName: "my-floci-ui");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var uiResource = appModel.Resources.OfType<FlociUIContainerResource>().SingleOrDefault();

        Assert.NotNull(uiResource);
        Assert.Equal("my-floci-ui", uiResource.Name);
    }

    [Fact]
    public void WithFlociUICalledTwiceOnSameResourceAddsSingleUIContainer()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        bool configureCallbackInvoked = false;
        builder.AddFloci("floci")
            .WithFlociUI()
            .WithFlociUI(configureContainer: _ => configureCallbackInvoked = true);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Single(appModel.Resources.OfType<FlociUIContainerResource>());
        Assert.True(configureCallbackInvoked);
    }

    [Fact]
    public void WithFlociUIPerFlociResourceAddsSeparateUIContainers()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFloci("floci1").WithFlociUI();
        builder.AddFloci("floci2").WithFlociUI();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var uiResources = appModel.Resources.OfType<FlociUIContainerResource>().ToList();

        Assert.Equal(2, uiResources.Count);
        Assert.Contains(uiResources, r => r.Name == "floci1-ui");
        Assert.Contains(uiResources, r => r.Name == "floci2-ui");
    }

    [Fact]
    public void WithFlociUIWithHostPortSetsEndpointPort()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFloci("floci")
            .WithFlociUI(configureContainer: ui => ui.WithHostPort(14500));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var uiResource = appModel.Resources.OfType<FlociUIContainerResource>().SingleOrDefault();

        Assert.NotNull(uiResource);
        Assert.True(uiResource.TryGetLastAnnotation(out EndpointAnnotation? endpointAnnotation));
        Assert.Equal(14500, endpointAnnotation.Port);
    }

    [Fact]
    public void AddFlociBuilderWithDataBindMountSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFloci("floci")
            .WithDataBindMount("floci-data", isReadOnly: false);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotations));
        Assert.Equal(ContainerMountType.BindMount, mountAnnotations.Type);
        Assert.Equal("/app/data", mountAnnotations.Target);
        Assert.NotNull(mountAnnotations.Source);
    }
}
