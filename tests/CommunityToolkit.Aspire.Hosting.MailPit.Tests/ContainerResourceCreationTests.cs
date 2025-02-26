using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.MailPit.Tests;

public class ContainerResourceCreationTests
{
    [Fact]
    public void AddMailPitBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<NullReferenceException>(() => builder.AddMailPit("mailpit"));
    }

    [Fact]
    public void AddMailPitBuilderNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
    
        Assert.Throws<ArgumentNullException>(() => builder.AddMailPit(null!));
    }

    [Fact]
    public void AddMailPitBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
    
        builder.AddMailPit("mailpit");
        
        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        var resource = appModel.Resources.OfType<MailPitContainerResource>().SingleOrDefault();
        
        Assert.NotNull(resource);
        Assert.Equal("mailpit", resource.Name);
        
        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(MailPitContainerImageTags.Tag, imageAnnotations.Tag);
        Assert.Equal(MailPitContainerImageTags.Image, imageAnnotations.Image);
        Assert.Equal(MailPitContainerImageTags.Registry, imageAnnotations.Registry);
    }
    
    [Fact]
    public void AddMailPitBuilderContainerWithDataVolumeDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddMailPit("mailpit")
            .WithDataVolume("mailpit-data", isReadOnly: false);
        
        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        var resource = appModel.Resources.OfType<MailPitContainerResource>().SingleOrDefault();
        
        Assert.NotNull(resource);
        Assert.Equal("mailpit", resource.Name);
        
        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(MailPitContainerImageTags.Tag, imageAnnotations.Tag);
        Assert.Equal(MailPitContainerImageTags.Image, imageAnnotations.Image);
        Assert.Equal(MailPitContainerImageTags.Registry, imageAnnotations.Registry);
        
        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotations));
        Assert.Equal(ContainerMountType.Volume, mountAnnotations.Type);
        Assert.Equal("/data", mountAnnotations.Target);
    }
    
    [Fact]
    public void AddMailPitBuilderContainerWithDataMountDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddMailPit("mailpit")
            .WithDataBindMount("mailpit-data", isReadOnly: false);
        
        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        var resource = appModel.Resources.OfType<MailPitContainerResource>().SingleOrDefault();
        
        Assert.NotNull(resource);
        Assert.Equal("mailpit", resource.Name);
        
        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(MailPitContainerImageTags.Tag, imageAnnotations.Tag);
        Assert.Equal(MailPitContainerImageTags.Image, imageAnnotations.Image);
        Assert.Equal(MailPitContainerImageTags.Registry, imageAnnotations.Registry);
        
        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotations));
        Assert.Equal(ContainerMountType.BindMount, mountAnnotations.Type);
        Assert.Equal("/data", mountAnnotations.Target);
        Assert.NotNull(mountAnnotations.Source);
    }
}

