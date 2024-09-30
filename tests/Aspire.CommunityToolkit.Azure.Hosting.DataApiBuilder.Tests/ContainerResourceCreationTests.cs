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
        Assert.Throws<ArgumentException>(() => builder.AddDataAPIBuilder(""));
    }



    [Fact]
    public void AddDataAPIBuilderContainerImageNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder("dab", new DataApiBuilderContainerResourceOptions { ContainerImageName = null! }));
        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder("dab", new DataApiBuilderContainerResourceOptions { ContainerImageName = "" }));
    }

    [Fact]
    public void AddDataAPIBuilderContainerResourceOptionsCanBeNull()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.NotNull(() => builder.AddDataAPIBuilder("dab", null!));
    }


    [Fact]
    public async Task AddDataAPIBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var options = new DataApiBuilderContainerResourceOptions
        {
            ContainerImageName = "azure-databases/data-api-builder",
            ContainerRegistry = "mcr.microsoft.com",
            ContainerImageTag = "latest",
            Port = 5000,
            TargetPort = 5000
        };

        builder.AddDataAPIBuilder("dab", options);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<DataApiBuilderContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("dab", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(options.ContainerImageName, imageAnnotations.Image);
        Assert.Equal(options.ContainerRegistry, imageAnnotations.Registry);
        Assert.Equal(options.ContainerImageTag, imageAnnotations.Tag);

        Assert.True(resource.TryGetLastAnnotation(out EndpointAnnotation? httpEndpointAnnotations));
        Assert.Equal(options.Port, httpEndpointAnnotations.Port);
        Assert.Equal(options.TargetPort, httpEndpointAnnotations.TargetPort);
    }
}
