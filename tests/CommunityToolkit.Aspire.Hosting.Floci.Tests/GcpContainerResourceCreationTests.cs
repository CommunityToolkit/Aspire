using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

public class GcpContainerResourceCreationTests
{
    [Fact]
    public void AddFlociGcpBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddFlociGcp("floci-gcp"));
    }

    [Fact]
    public void AddFlociGcpBuilderNameShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddFlociGcp(null!));
    }

    [Fact]
    public void AddFlociGcpBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociGcp("floci-gcp");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociGcpContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("floci-gcp", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(FlociContainerImageTags.GcpTag, imageAnnotations.Tag);
        Assert.Equal(FlociContainerImageTags.GcpImage, imageAnnotations.Image);
        Assert.Equal(FlociContainerImageTags.GcpRegistry, imageAnnotations.Registry);

        Assert.True(resource.TryGetLastAnnotation(out EndpointAnnotation? endpointAnnotation));
        Assert.Equal(4588, endpointAnnotation.TargetPort);
        Assert.Equal("gcp", endpointAnnotation.Name);
    }

    [Fact]
    public async Task AddFlociGcpBuilderSetsEnvironmentVariables()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociGcp("floci-gcp", defaultProjectId: "my-project");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociGcpContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<EnvironmentCallbackAnnotation>? envAnnotations));

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var annotation in envAnnotations!)
        {
            await annotation.Callback(context);
        }

        Assert.Equal("floci-gcp", envVars[FlociGcpContainerResource.HostnameEnvVar].ToString());
        Assert.Equal("my-project", envVars[FlociGcpContainerResource.DefaultProjectIdEnvVar].ToString());
        Assert.Equal("memory", envVars[resource.StorageModeEnvVar].ToString());
    }

    [Fact]
    public void AddFlociGcpBuilderWithDataVolumeSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociGcp("floci-gcp")
            .WithDataVolume("floci-gcp-data", isReadOnly: false);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociGcpContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotations));
        Assert.Equal(ContainerMountType.Volume, mountAnnotations.Type);
        Assert.Equal("/app/data", mountAnnotations.Target);
    }

    [Fact]
    public void AddFlociGcpBuilderWithDataBindMountSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociGcp("floci-gcp")
            .WithDataBindMount("floci-gcp-data", isReadOnly: false);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociGcpContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotations));
        Assert.Equal(ContainerMountType.BindMount, mountAnnotations.Type);
        Assert.Equal("/app/data", mountAnnotations.Target);
        Assert.NotNull(mountAnnotations.Source);
    }

    [Fact]
    public void WithFlociUIBuilderShouldNotBeNullForGcp()
    {
        IResourceBuilder<FlociGcpContainerResource> builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.WithFlociUI());
    }

    [Fact]
    public void WithFlociUIAddsUIContainerResourceForGcp()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociGcp("floci-gcp")
            .WithFlociUI();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var flociResource = appModel.Resources.OfType<FlociGcpContainerResource>().SingleOrDefault();
        var uiResource = appModel.Resources.OfType<FlociUIContainerResource>().SingleOrDefault();

        Assert.NotNull(flociResource);
        Assert.NotNull(uiResource);
        Assert.Equal("floci-gcp-ui", uiResource.Name);
        Assert.Same(flociResource, uiResource.Parent);

        Assert.True(uiResource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(FlociContainerImageTags.UITag, imageAnnotations.Tag);
        Assert.Equal(FlociContainerImageTags.UIImage, imageAnnotations.Image);
        Assert.Equal(FlociContainerImageTags.UIRegistry, imageAnnotations.Registry);
    }

    [Fact]
    public async Task WithFlociUISetsEnvironmentVariablesForGcp()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociGcp("floci-gcp", defaultProjectId: "my-project")
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

        var endpointExpression = Assert.IsType<ReferenceExpression>(envVars[FlociUIContainerResource.GcpEndpointEnvVar]);
        Assert.Contains("floci-gcp.bindings.gcp.url", endpointExpression.ValueExpression);
        Assert.Equal("my-project", envVars[FlociUIContainerResource.GcpProjectEnvVar].ToString());
    }

    [Fact]
    public void WithFlociUICalledTwiceOnSameResourceAddsSingleUIContainerForGcp()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        bool configureCallbackInvoked = false;
        builder.AddFlociGcp("floci-gcp")
            .WithFlociUI()
            .WithFlociUI(configureContainer: _ => configureCallbackInvoked = true);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Single(appModel.Resources.OfType<FlociUIContainerResource>());
        Assert.True(configureCallbackInvoked);
    }
}
