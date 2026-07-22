using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

public class AzureContainerResourceCreationTests
{
    [Fact]
    public void AddFlociAzureBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddFlociAzure("floci-az"));
    }

    [Fact]
    public void AddFlociAzureBuilderNameShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddFlociAzure(null!));
    }

    [Fact]
    public void AddFlociAzureBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociAzure("floci-az");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociAzureContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("floci-az", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(FlociContainerImageTags.AzureTag, imageAnnotations.Tag);
        Assert.Equal(FlociContainerImageTags.AzureImage, imageAnnotations.Image);
        Assert.Equal(FlociContainerImageTags.AzureRegistry, imageAnnotations.Registry);

        Assert.True(resource.TryGetLastAnnotation(out EndpointAnnotation? endpointAnnotation));
        Assert.Equal(4577, endpointAnnotation.TargetPort);
        Assert.Equal("azure", endpointAnnotation.Name);
    }

    [Fact]
    public async Task AddFlociAzureBuilderSetsEnvironmentVariables()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociAzure("floci-az");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociAzureContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<EnvironmentCallbackAnnotation>? envAnnotations));

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var annotation in envAnnotations!)
        {
            await annotation.Callback(context);
        }

        Assert.Equal("floci-az", envVars[FlociAzureContainerResource.HostnameEnvVar].ToString());
        Assert.Equal("memory", envVars[resource.StorageModeEnvVar].ToString());
    }

    [Fact]
    public void AddFlociAzureBuilderWithDataVolumeSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociAzure("floci-az")
            .WithDataVolume("floci-az-data", isReadOnly: false);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociAzureContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotations));
        Assert.Equal(ContainerMountType.Volume, mountAnnotations.Type);
        Assert.Equal("/app/data", mountAnnotations.Target);
    }

    [Fact]
    public void AddFlociAzureBuilderWithDataBindMountSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociAzure("floci-az")
            .WithDataBindMount("floci-az-data", isReadOnly: false);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociAzureContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotations));
        Assert.Equal(ContainerMountType.BindMount, mountAnnotations.Type);
        Assert.Equal("/app/data", mountAnnotations.Target);
        Assert.NotNull(mountAnnotations.Source);
    }

    [Fact]
    public void WithFlociUIBuilderShouldNotBeNullForAzure()
    {
        IResourceBuilder<FlociAzureContainerResource> builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.WithFlociUI());
    }

    [Fact]
    public void WithFlociUIAddsUIContainerResourceForAzure()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociAzure("floci-az")
            .WithFlociUI();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var flociResource = appModel.Resources.OfType<FlociAzureContainerResource>().SingleOrDefault();
        var uiResource = appModel.Resources.OfType<FlociUIContainerResource>().SingleOrDefault();

        Assert.NotNull(flociResource);
        Assert.NotNull(uiResource);
        Assert.Equal("floci-az-ui", uiResource.Name);
        Assert.Same(flociResource, uiResource.Parent);

        Assert.True(uiResource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(FlociContainerImageTags.UITag, imageAnnotations.Tag);
        Assert.Equal(FlociContainerImageTags.UIImage, imageAnnotations.Image);
        Assert.Equal(FlociContainerImageTags.UIRegistry, imageAnnotations.Registry);
    }

    [Fact]
    public async Task WithFlociUISetsEnvironmentVariablesForAzure()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociAzure("floci-az")
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

        var endpointExpression = Assert.IsType<ReferenceExpression>(envVars[FlociUIContainerResource.AzureEndpointEnvVar]);
        Assert.Contains("floci-az.bindings.azure.url", endpointExpression.ValueExpression);
        Assert.Equal("devstoreaccount1", envVars[FlociUIContainerResource.AzureAccountNameEnvVar].ToString());
    }

    [Fact]
    public void WithFlociUICalledTwiceOnSameResourceAddsSingleUIContainerForAzure()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        bool configureCallbackInvoked = false;
        builder.AddFlociAzure("floci-az")
            .WithFlociUI()
            .WithFlociUI(configureContainer: _ => configureCallbackInvoked = true);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Single(appModel.Resources.OfType<FlociUIContainerResource>());
        Assert.True(configureCallbackInvoked);
    }
}
