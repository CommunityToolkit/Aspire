using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.Tests;

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
        Assert.Equal("7.0.0-rc1", imageAnnotations.Tag);
        Assert.Equal("changemakerstudiosus/papercut-smtp", imageAnnotations.Image);
        Assert.Equal("docker.io", imageAnnotations.Registry);
    }
}
