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
