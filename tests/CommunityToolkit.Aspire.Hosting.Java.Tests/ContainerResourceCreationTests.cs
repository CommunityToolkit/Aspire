namespace Aspire.CommunityToolkit.Hosting.Java.Tests;
public class ContainerResourceCreationTests
{
    [Fact]
    public void AddJavaAppBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp("java", new JavaAppContainerResourceOptions()));
    }

    [Fact]
    public void AddSpringAppBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddSpringApp("spring", new JavaAppContainerResourceOptions()));
    }

    [Fact]
    public void AddJavaAppNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp(null!, new JavaAppContainerResourceOptions()));
        Assert.Throws<ArgumentException>(() => builder.AddJavaApp("", new JavaAppContainerResourceOptions()));
    }

    [Fact]
    public void AddSpringAppNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddSpringApp(null!, new JavaAppContainerResourceOptions()));
        Assert.Throws<ArgumentException>(() => builder.AddSpringApp("", new JavaAppContainerResourceOptions()));
    }

    [Fact]
    public void AddJavaAppContainerImageNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp("java", new JavaAppContainerResourceOptions { ContainerImageName = null! }));
        Assert.Throws<ArgumentException>(() => builder.AddJavaApp("java", new JavaAppContainerResourceOptions { ContainerImageName = "" }));
    }

    [Fact]
    public void AddJavaAppContainerResourceOptionsShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp("java", null!));
    }

    [Fact]
    public void AddSpringAppContainerResourceOptionsShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddSpringApp("spring", null!));
    }

    [Fact]
    public async Task AddJavaAppContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var options = new JavaAppContainerResourceOptions
        {
            ContainerImageName = "java-app",
            ContainerRegistry = "docker.io",
            ContainerImageTag = "latest",
            Port = 8080,
            TargetPort = 8080,
            OtelAgentPath = "path/to/otel",
            Args = ["arg1", "arg2"]
        };

        builder.AddJavaApp("java", options);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<JavaAppContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("java", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(options.ContainerImageName, imageAnnotations.Image);
        Assert.Equal(options.ContainerRegistry, imageAnnotations.Registry);
        Assert.Equal(options.ContainerImageTag, imageAnnotations.Tag);

        Assert.True(resource.TryGetLastAnnotation(out EndpointAnnotation? httpEndpointAnnotations));
        Assert.Equal(options.Port, httpEndpointAnnotations.Port);
        Assert.Equal(options.TargetPort, httpEndpointAnnotations.TargetPort);

        Assert.True(resource.TryGetLastAnnotation(out CommandLineArgsCallbackAnnotation? argsAnnotations));
        CommandLineArgsCallbackContext context = new([]);
        await argsAnnotations.Callback(context);
        Assert.All(options.Args, arg => Assert.Contains(arg, context.Args));
    }
}
