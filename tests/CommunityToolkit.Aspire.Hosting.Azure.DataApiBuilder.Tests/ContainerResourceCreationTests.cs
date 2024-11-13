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

        // verify ports

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpoints));

        var http = endpoints.Where(x => x.Name == DataApiBuilderContainerResource.HttpEndpointName).Single();
        Assert.Equal(DataApiBuilderContainerResource.HttpEndpointPort, http.TargetPort);

        // var https = endpoints.Where(x => x.Name == DataApiBuilderContainerResource.HttpsEndpointName).Single();
        // Assert.Equal(DataApiBuilderContainerResource.HttpsEndpointPort, https.TargetPort);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_DefaultFile_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // defaults to ./dab-config.json which exists in this test project root
        builder.AddDataAPIBuilder("dab");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        var annotation = Assert.Single(configFileAnnotations);
        Assert.EndsWith("/dab-config.json", annotation.Source);
        Assert.Equal("/App/dab-config.json", annotation.Target);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_PortOnly_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", httpPort: 1234);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpointAnnotations));

        var annotation = Assert.Single(endpointAnnotations);
        Assert.Equal(1234, annotation.Port);
        Assert.Equal(DataApiBuilderContainerResource.HttpEndpointPort, annotation.TargetPort);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ValidFile_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // file exists in test project root
        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        var annotation = Assert.Single(configFileAnnotations);
        Assert.EndsWith("/dab-config.json", annotation.Source);
        Assert.Equal("/App/dab-config.json", annotation.Target);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ValidFileWithPort_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // file exists in test project root
        builder.AddDataAPIBuilder("dab", httpPort: 1234, configFilePaths: "./dab-config.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpointAnnotations));

        var annotation = Assert.Single(endpointAnnotations);
        Assert.Equal(1234, annotation.Port);
        Assert.Equal(DataApiBuilderContainerResource.HttpEndpointPort, annotation.TargetPort);

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        var configAnnotation = Assert.Single(configFileAnnotations);
        Assert.EndsWith("/dab-config.json", configAnnotation.Source);
        Assert.Equal("/App/dab-config.json", configAnnotation.Target);
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

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        Assert.Equal(2, configFileAnnotations.Count());
        Assert.Collection(
            configFileAnnotations,
            a =>
            {
                Assert.EndsWith("/dab-config.json", a.Source);
                Assert.Equal("/App/dab-config.json", a.Target);
            },
            a =>
            {
                Assert.EndsWith("/dab-config-2.json", a.Source);
                Assert.Equal("/App/dab-config-2.json", a.Target);
            });
    }

    [Fact]
    public void AddDataAPIBuilderContainer_InvalidFiles_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // some (not all) files exist in test project root
        Assert.Throws<FileNotFoundException>(() => builder.AddDataAPIBuilder("dab", "./dab-config.json", "./dab-config-2.json", Guid.NewGuid().ToString()));
    }
}
