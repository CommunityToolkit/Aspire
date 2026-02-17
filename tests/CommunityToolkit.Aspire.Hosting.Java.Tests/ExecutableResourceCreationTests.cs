using System.Runtime.InteropServices;
using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Java.Tests;

public class ExecutableResourceCreationTests
{
    [Fact]
    public void AddJavaAppBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp("java", Environment.CurrentDirectory));
    }

    [Fact]
    public void AddJavaAppNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp(null!, Environment.CurrentDirectory));

        const string name = "";
        Assert.Throws<ArgumentException>(() => builder.AddJavaApp(name, Environment.CurrentDirectory));
    }

    [Fact]
    public void AddJavaAppWorkingDirectoryShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp("java", workingDirectory: null!));
        Assert.Throws<ArgumentException>(() => builder.AddJavaApp("java", workingDirectory: ""));
    }

    [Fact]
    public async Task AddJavaAppDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("java", Environment.CurrentDirectory, "test.jar", args: ["--test"])
               .WithJvmArgs("-Dtest");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<JavaAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("java", resource.Name);

        Assert.Equal(Environment.CurrentDirectory, resource.WorkingDirectory);
        Assert.Equal("java", resource.Command);

        var args = await resource.GetArgumentListAsync();
        Assert.Equal("-Dtest", args[0]);
        Assert.Contains("-jar", args);
        Assert.Contains("test.jar", args);
        Assert.Contains("--test", args);
    }

    [Fact]
    public void AddJavaAppWithMavenBuildCreatesResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("java", Environment.CurrentDirectory)
            .WithMavenBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var javaResource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var buildResource = Assert.Single(appModel.Resources.OfType<MavenBuildResource>());

        Assert.Equal("java-maven-build", buildResource.Name);
        Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : Path.Combine(javaResource.WorkingDirectory, "mvnw"), buildResource.Command);
        
        Assert.True(javaResource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        Assert.Contains(waitAnnotations, w => w.Resource == buildResource);
    }

    [Fact]
    public void AddJavaAppWithGradleBuildCreatesResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("java", Environment.CurrentDirectory)
            .WithGradleBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var javaResource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var buildResource = Assert.Single(appModel.Resources.OfType<GradleBuildResource>());

        Assert.Equal("java-gradle-build", buildResource.Name);
        Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : Path.Combine(javaResource.WorkingDirectory, "gradlew"), buildResource.Command);
        
        Assert.True(javaResource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        Assert.Contains(waitAnnotations, w => w.Resource == buildResource);
    }

    [Fact]
    public async Task AddJavaAppWithOtelAgentSetsEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("java", Environment.CurrentDirectory)
            .WithOtelAgent("/agents/opentelemetry-javaagent.jar");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());

        var config = await resource.GetEnvironmentVariablesAsync();
        Assert.True(config.TryGetValue("JAVA_TOOL_OPTIONS", out var value));
        Assert.Equal("-javaagent:/agents/opentelemetry-javaagent.jar", value);
    }

    [Fact]
#pragma warning disable CS0618
    public void DeprecatedAddJavaAppBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp("java", Environment.CurrentDirectory, new JavaAppExecutableResourceOptions()));
    }
#pragma warning restore CS0618
}
