namespace Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder.Tests;
public class ContainerResourceCreationTests
{
    [Fact]
    public void AddDataAPIBuilderBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<NullReferenceException>(() => builder.AddDataAPIBuilder("dab"));
    }

    [Fact]
    public void AddDataApiBuilderNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder(null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder(""));
    }

    [Fact]
    public void AddDataApiBuilderConfigFilePathShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder("dab", configFilePath: null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder("dab", configFilePath: ""));
    }

    [Fact]
    public void AddDataAPIBuilderContainerRegistryShouldNotBeNullOrWhitespace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder("dab", containerRegistry: null! ));
        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder("dab", containerRegistry: "" ));
    }

    [Fact]
    public void AddDataAPIBuilderContainerImageNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder("dab", containerImageName: null! ));
        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder("dab", containerImageName: "" ));
    }

    [Fact]
    public void AddDataAPIBuilderContainerImageTagNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder("dab", containerImageTag: null! ));
        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder("dab", containerImageTag: "" ));
    }


    [Fact]
    public void AddDataAPIBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var containerImageName = "azure-databases/data-api-builder";
        var containerRegistry = "mcr.microsoft.com";
        var containerImageTag = "latest";
        var port = 5000;
        var targetPort = 5000;

        builder.AddDataAPIBuilder("dab", containerRegistry: containerRegistry, containerImageName: containerImageName, containerImageTag: containerImageTag, port: port, targetPort: targetPort);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<DataApiBuilderContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("dab", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(containerImageName, imageAnnotations.Image);
        Assert.Equal(containerRegistry, imageAnnotations.Registry);
        Assert.Equal(containerImageTag, imageAnnotations.Tag);

        Assert.True(resource.TryGetLastAnnotation(out EndpointAnnotation? httpEndpointAnnotations));
        Assert.Equal(port, httpEndpointAnnotations.Port);
        Assert.Equal(targetPort, httpEndpointAnnotations.TargetPort);
    }
}
