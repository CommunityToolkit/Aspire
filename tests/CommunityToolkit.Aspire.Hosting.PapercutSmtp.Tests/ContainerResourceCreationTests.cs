using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.PapercutSmtp.Tests;

public class ContainerResourceCreationTests
{
    [Fact]
    public void AddPapercutSmtpBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<NullReferenceException>(() => builder.AddPapercutSmtp("papercut"));
    }

    [Fact]
    public void AddPapercutSmtpBuilderNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
    
        Assert.Throws<ArgumentNullException>(() => builder.AddPapercutSmtp(null!));
    }

    [Fact]
    public void AddPapercutSmtpBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
    
        builder.AddPapercutSmtp("papercut");
        
        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        var resource = appModel.Resources.OfType<PapercutSmtpContainerResource>().SingleOrDefault();
        
        Assert.NotNull(resource);
        Assert.Equal("papercut", resource.Name);
        
        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(PapercutSmtpContainerImageTags.Tag, imageAnnotations.Tag);
        Assert.Equal(PapercutSmtpContainerImageTags.Image, imageAnnotations.Image);
        Assert.Equal(PapercutSmtpContainerImageTags.Registry, imageAnnotations.Registry);
    }
}
