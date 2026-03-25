using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.PapercutSmtp.Tests;

public class ContainerResourceCreationTests
{
    [Fact]
    public void AddPapercutSmtpBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => builder.AddPapercutSmtp("papercut"));
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddPapercutSmtpBuilderNameShouldNotBeNullOrWhiteSpace(string? name)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        ArgumentException exception = Assert.ThrowsAny<ArgumentException>(() => builder.AddPapercutSmtp(name!));
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddPapercutSmtpBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddPapercutSmtp("papercut");

        using var app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        PapercutSmtpContainerResource? resource = appModel.Resources.OfType<PapercutSmtpContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("papercut", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(PapercutSmtpContainerImageTags.Tag, imageAnnotations.Tag);
        Assert.Equal(PapercutSmtpContainerImageTags.Image, imageAnnotations.Image);
        Assert.Equal(PapercutSmtpContainerImageTags.Registry, imageAnnotations.Registry);
    }
}
