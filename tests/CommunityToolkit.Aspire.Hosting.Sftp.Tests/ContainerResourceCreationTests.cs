using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Sftp.Tests;

public class ContainerResourceCreationTests
{
    [Fact]
    public void AddSftpBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<NullReferenceException>(() => builder.AddSftp("sftp"));
    }

    [Fact]
    public void AddSftpBuilderNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
    
        Assert.Throws<ArgumentNullException>(() => builder.AddSftp(null!));
    }

    [Fact]
    public void AddSftpBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
    
        builder.AddSftp("sftp");
        
        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        var resource = appModel.Resources.OfType<SftpContainerResource>().SingleOrDefault();
        
        Assert.NotNull(resource);
        Assert.Equal("sftp", resource.Name);
        
        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(SftpContainerImageTags.Tag, imageAnnotations.Tag);
        Assert.Equal(SftpContainerImageTags.Image, imageAnnotations.Image);
        Assert.Equal(SftpContainerImageTags.Registry, imageAnnotations.Registry);
    }
}
