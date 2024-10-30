using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.Tests;
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
    }

    [Fact]
    public void AddDataAPIBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<DataApiBuilderContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("dab", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));

        Assert.True(resource.TryGetLastAnnotation(out EndpointAnnotation? httpEndpointAnnotations));
        Assert.Equal(5000, httpEndpointAnnotations.TargetPort);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_DefaultFile_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // defaults to ./dab-config.json which exists in this test project root
        builder.AddDataAPIBuilder("dab");
    }

    [Fact]
    public void AddDataAPIBuilderContainer_PortOnly_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", port: 1234);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ValidFile_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // file exists in test project root
        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config.json");
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ValidFileWithPort_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // file exists in test project root
        builder.AddDataAPIBuilder("dab", port: 1234, configFilePaths: "./dab-config.json");
    }

    [Fact]
    public void AddDataAPIBuilderContainer_InvalidFile_ThrowsEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // file does not exist in test project root
        Assert.Throws<FileNotFoundException>(() => builder.AddDataAPIBuilder("dab", configFilePaths: Guid.NewGuid().ToString()));
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ValidFiles_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // both files exist in test project root
        builder.AddDataAPIBuilder("dab", "./dab-config.json", "./dab-config-2.json");
    }

    [Fact]
    public void AddDataAPIBuilderContainer_InvalidFiles_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // some (not all) files exist in test project root
        Assert.Throws<FileNotFoundException>(() => builder.AddDataAPIBuilder("dab", "./dab-config.json", "./dab-config-2.json", Guid.NewGuid().ToString()));
    }
}
