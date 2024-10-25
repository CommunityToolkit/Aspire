using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Java.Tests;

public class ExecutableResourceCreationTests
{
    [Fact]
    public void AddJavaAppBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp("java", Environment.CurrentDirectory, new JavaAppExecutableResourceOptions()));
    }

    [Fact]
    public void AddSpringAppBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddSpringApp("spring", Environment.CurrentDirectory, new JavaAppExecutableResourceOptions()));
    }

    [Fact]
    public void AddJavaAppNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp(null!, Environment.CurrentDirectory, new JavaAppExecutableResourceOptions()));

        const string name = "";
        Assert.Throws<ArgumentException>(() => builder.AddJavaApp(name, Environment.CurrentDirectory, new JavaAppExecutableResourceOptions()));
    }

    [Fact]
    public void AddSpringAppNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddSpringApp(null!, Environment.CurrentDirectory, new JavaAppExecutableResourceOptions()));

        const string name = "";
        Assert.Throws<ArgumentException>(() => builder.AddSpringApp(name, Environment.CurrentDirectory, new JavaAppExecutableResourceOptions()));
    }

    [Fact]
    public void AddJavaAppWorkingDirectoryShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp("java", null!, new JavaAppExecutableResourceOptions()));
        Assert.Throws<ArgumentException>(() => builder.AddJavaApp("java", "", new JavaAppExecutableResourceOptions()));
    }

    [Fact]
    public void AddJavaAppExecutableResourceOptionsShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp("java", Environment.CurrentDirectory, null!));
    }

    [Fact]
    public void AddSpringAppContainerResourceOptionsShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddSpringApp("spring", Environment.CurrentDirectory, null!));
    }

    [Fact]
    public async Task AddJavaAppContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var options = new JavaAppExecutableResourceOptions
        {
            ApplicationName = "test.jar",
            Args = ["--test"],
            OtelAgentPath = "otel-agent",
            Port = 8080
        };

        builder.AddJavaApp("java", Environment.CurrentDirectory, options);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<JavaAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("java", resource.Name);

        Assert.Equal(Environment.CurrentDirectory, resource.WorkingDirectory);
        Assert.Equal("java", resource.Command);

        Assert.True(resource.TryGetLastAnnotation(out EndpointAnnotation? httpEndpointAnnotations));
        Assert.Equal(options.Port, httpEndpointAnnotations.Port);

        Assert.True(resource.TryGetLastAnnotation(out CommandLineArgsCallbackAnnotation? argsAnnotations));
        CommandLineArgsCallbackContext context = new([]);
        await argsAnnotations.Callback(context);
        Assert.All(options.Args, arg => Assert.Contains(arg, context.Args));
        Assert.Contains("-jar", context.Args);
        Assert.Contains(options.ApplicationName, context.Args);
    }
}
