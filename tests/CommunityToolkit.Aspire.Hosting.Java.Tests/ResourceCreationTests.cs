using System.Runtime.InteropServices;
using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Java.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void DefaultJavaApp()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("javaapp", "../java-project");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<JavaAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("java", resource.Command);
    }

    [Fact]
    public async Task JavaAppWithJarPathHasCorrectArgsAsync()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("javaapp", "../java-project", "app.jar");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<JavaAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);

        var args = await resource.GetArgumentListAsync();
        Assert.Contains("-jar", args);
        Assert.Contains("app.jar", args);
    }

    [Fact]
    public void JavaAppWithMavenBuildCreatesBuildResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("javaapp", "../java-project").WithMavenBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var javaResource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var buildResource = Assert.Single(appModel.Resources.OfType<MavenBuildResource>());

        Assert.Equal("javaapp-maven-build", buildResource.Name);
        Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : Path.Combine(javaResource.WorkingDirectory, "mvnw"), buildResource.Command);

        // Verify that the Java app waits for the build to complete
        Assert.True(javaResource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        Assert.Contains(waitAnnotations, w => w.Resource == buildResource);
    }

    [Fact]
    public async Task AddingMavenBuildWithOverrides()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("java", Environment.CurrentDirectory)
            .WithMavenBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var buildResource = Assert.Single(appModel.Resources.OfType<MavenBuildResource>());

        var args = await buildResource.GetArgumentListAsync();
        string[] expectedArgs = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ["/c", "mvnw.cmd", "clean", "package"] : ["clean", "package"];
        Assert.Equal(expectedArgs, args);
    }

    [Fact]
    public void JavaAppWithGradleBuildCreatesBuildResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("javaapp", "../java-project").WithGradleBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var javaResource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var buildResource = Assert.Single(appModel.Resources.OfType<GradleBuildResource>());

        Assert.Equal("javaapp-gradle-build", buildResource.Name);
        Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : Path.Combine(javaResource.WorkingDirectory, "gradlew"), buildResource.Command);

        // Verify that the Java app waits for the build to complete
        Assert.True(javaResource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        Assert.Contains(waitAnnotations, w => w.Resource == buildResource);
    }

    [Fact]
    public async Task JavaAppWithJvmArgsHasCorrectArgsAsync()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("javaapp", "../java-project")
               .WithJvmArgs("-Xmx512m");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());

        var args = await resource.GetArgumentListAsync();
        Assert.Equal("-Xmx512m", args[0]);
        Assert.Contains("-jar", args);
        Assert.Contains("target/app.jar", args);
    }
}
