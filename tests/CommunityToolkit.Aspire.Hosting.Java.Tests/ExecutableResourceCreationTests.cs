using Aspire.Hosting;
using Aspire.Hosting.Eventing;

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

    [Fact]
    public void AddingMavenOptions()
    {
        var builder = DistributedApplication.CreateBuilder();

        var options = new JavaAppExecutableResourceOptions
        {
            ApplicationName = "test.jar",
            Args = ["--test"],
            OtelAgentPath = "otel-agent",
            Port = 8080
        };

        builder.AddJavaApp("java", Environment.CurrentDirectory, options)
            .WithMavenBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<MavenBuildAnnotation>());

        Assert.NotNull(annotation.MavenOptions);
        Assert.Equal("mvnw", annotation.MavenOptions.Command);
        Assert.Equal("--quiet clean package", string.Join(' ', annotation.MavenOptions.Args));
        Assert.Equal(Environment.CurrentDirectory, annotation.MavenOptions.WorkingDirectory);
    }

    [Fact]
    public void AddingMavenOptionsWithOverrides()
    {
        var builder = DistributedApplication.CreateBuilder();

        var options = new JavaAppExecutableResourceOptions
        {
            ApplicationName = "test.jar",
            Args = ["--test"],
            OtelAgentPath = "otel-agent",
            Port = 8080
        };

        builder.AddJavaApp("java", Environment.CurrentDirectory, options)
            .WithMavenBuild(new()
            {
                Args = ["clean", "package"],
            });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<MavenBuildAnnotation>());

        Assert.NotNull(annotation.MavenOptions);
        Assert.Equal("mvnw", annotation.MavenOptions.Command);
        Assert.Equal("clean package", string.Join(' ', annotation.MavenOptions.Args));
        Assert.Equal(Environment.CurrentDirectory, annotation.MavenOptions.WorkingDirectory);
    }

    [Fact]
    public void ChainingAddMavenBuildOverridesPreviousOptions()
    {
        var builder = DistributedApplication.CreateBuilder();

        var options = new JavaAppExecutableResourceOptions
        {
            ApplicationName = "test.jar",
            Args = ["--test"],
            OtelAgentPath = "otel-agent",
            Port = 8080
        };

        builder.AddJavaApp("java", Environment.CurrentDirectory, options)
            .WithMavenBuild(new()
            {
                Args = ["clean", "package"],
            })
            .WithMavenBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<MavenBuildAnnotation>());

        Assert.NotNull(annotation.MavenOptions);
        Assert.Equal("mvnw", annotation.MavenOptions.Command);
        Assert.Equal("--quiet clean package", string.Join(' ', annotation.MavenOptions.Args));
        Assert.Equal(Environment.CurrentDirectory, annotation.MavenOptions.WorkingDirectory);
    }

    [Fact]
    public void AddingMavenBuildRegistersRebuildCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        var options = new JavaAppExecutableResourceOptions
        {
            ApplicationName = "test.jar",
            Args = ["--test"],
            OtelAgentPath = "otel-agent",
            Port = 8080
        };

        builder.AddJavaApp("java", Environment.CurrentDirectory, options)
            .WithMavenBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        Assert.Single(resource.Annotations.OfType<ResourceCommandAnnotation>(), a => a.Name == "build-with-maven");
    }

    [Fact]
    public void MultipleAddingMavenBuildRegistersSingleRebuildCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        var options = new JavaAppExecutableResourceOptions
        {
            ApplicationName = "test.jar",
            Args = ["--test"],
            OtelAgentPath = "otel-agent",
            Port = 8080
        };

        builder.AddJavaApp("java", Environment.CurrentDirectory, options)
            .WithMavenBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        Assert.Single(resource.Annotations.OfType<ResourceCommandAnnotation>(), a => a.Name == "build-with-maven");
    }

    [Theory]
    [InlineData("Stopped", ResourceCommandState.Enabled)]
    [InlineData("Finished", ResourceCommandState.Enabled)]
    [InlineData("Exited", ResourceCommandState.Enabled)]
    [InlineData("FailedToStart", ResourceCommandState.Enabled)]
    [InlineData("Starting", ResourceCommandState.Disabled)]
    [InlineData("Running", ResourceCommandState.Disabled)]
    public void MavenBuildCommandAvailability(string text, ResourceCommandState expectedCommandState)
    {
        var builder = DistributedApplication.CreateBuilder();

        var options = new JavaAppExecutableResourceOptions
        {
            ApplicationName = "test.jar",
            Args = ["--test"],
            OtelAgentPath = "otel-agent",
            Port = 8080
        };

        builder.AddJavaApp("java", Environment.CurrentDirectory, options)
            .WithMavenBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var annoitation = Assert.Single(resource.Annotations.OfType<ResourceCommandAnnotation>(), a => a.Name == "build-with-maven");

        var updateState = annoitation.UpdateState(new UpdateCommandStateContext()
        {
            ResourceSnapshot = new CustomResourceSnapshot()
            {
                State = new ResourceStateSnapshot(text, null),
                ResourceType = "JavaAppExecutableResource",
                Properties = []
            },
            ServiceProvider = app.Services
        });
        Assert.Equal(expectedCommandState, updateState);
    }
}
