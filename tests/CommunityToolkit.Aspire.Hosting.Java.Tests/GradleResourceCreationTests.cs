using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Java.Tests;

public class GradleResourceCreationTests
{
    [Fact]
    public void AddingGradleOptions()
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
            .WithGradleBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<GradleBuildAnnotation>());

        Assert.NotNull(annotation.GradleOptions);
        Assert.Equal("gradlew", annotation.GradleOptions.Command);
        Assert.Equal("--quiet clean build", string.Join(' ', annotation.GradleOptions.Args));
        Assert.Equal(Environment.CurrentDirectory, annotation.GradleOptions.WorkingDirectory);
    }

    [Fact]
    public void AddingGradleOptionsWithOverrides()
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
            .WithGradleBuild(new()
            {
                Args = ["clean", "build"],
            });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<GradleBuildAnnotation>());

        Assert.NotNull(annotation.GradleOptions);
        Assert.Equal("gradlew", annotation.GradleOptions.Command);
        Assert.Equal("clean build", string.Join(' ', annotation.GradleOptions.Args));
        Assert.Equal(Environment.CurrentDirectory, annotation.GradleOptions.WorkingDirectory);
    }

    [Fact]
    public void ChainingAddGradleBuildOverridesPreviousOptions()
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
            .WithGradleBuild(new()
            {
                Args = ["clean", "build"],
            })
            .WithGradleBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<GradleBuildAnnotation>());

        Assert.NotNull(annotation.GradleOptions);
        Assert.Equal("gradlew", annotation.GradleOptions.Command);
        Assert.Equal("--quiet clean build", string.Join(' ', annotation.GradleOptions.Args));
        Assert.Equal(Environment.CurrentDirectory, annotation.GradleOptions.WorkingDirectory);
    }

    [Fact]
    public void AddingGradleBuildRegistersRebuildCommand()
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
            .WithGradleBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        Assert.Single(resource.Annotations.OfType<ResourceCommandAnnotation>(), a => a.Name == "build-with-gradle");
    }

    [Fact]
    public void MultipleAddingGradleBuildRegistersSingleRebuildCommand()
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
            .WithGradleBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        Assert.Single(resource.Annotations.OfType<ResourceCommandAnnotation>(), a => a.Name == "build-with-gradle");
    }

    [Theory]
    [InlineData("Stopped", ResourceCommandState.Enabled)]
    [InlineData("Finished", ResourceCommandState.Enabled)]
    [InlineData("Exited", ResourceCommandState.Enabled)]
    [InlineData("FailedToStart", ResourceCommandState.Enabled)]
    [InlineData("Starting", ResourceCommandState.Disabled)]
    [InlineData("Running", ResourceCommandState.Disabled)]
    public void GradleBuildCommandAvailability(string text, ResourceCommandState expectedCommandState)
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
            .WithGradleBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<ResourceCommandAnnotation>(), a => a.Name == "build-with-gradle");

        var updateState = annotation.UpdateState(new UpdateCommandStateContext()
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
