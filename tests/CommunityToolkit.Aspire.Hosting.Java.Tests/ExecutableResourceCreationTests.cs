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

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp("java", Environment.CurrentDirectory, "app.jar"));
    }

    [Fact]
    public void AddJavaAppNameShouldNotBeNullOrWhiteSpace()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp(null!, Environment.CurrentDirectory, "app.jar"));

        const string name = "";
        Assert.Throws<ArgumentException>(() => builder.AddJavaApp(name, Environment.CurrentDirectory, "app.jar"));
    }

    [Fact]
    public void AddJavaAppWorkingDirectoryShouldNotBeNullOrWhiteSpace()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddJavaApp("java", workingDirectory: null!, jarPath: "app.jar"));
        Assert.Throws<ArgumentException>(() => builder.AddJavaApp("java", workingDirectory: "", jarPath: "app.jar"));
    }

    [Fact]
    public void DefaultJavaApp()
    {
        var appModel = BuildAppModel(builder =>
            builder.AddJavaApp("javaapp", "../java-project", "target/app.jar"));

        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        Assert.Equal("java", resource.Command);
    }

    [Fact]
    public async Task AddJavaAppDetailsSetOnResource()
    {
        var appModel = BuildAppModel(builder =>
            builder.AddJavaApp("java", Environment.CurrentDirectory, "test.jar", args: ["--test"])
                   .WithJvmArgs("-Dtest"));

        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());

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
    public async Task JavaAppWithJvmArgsHasCorrectArgsAsync()
    {
        var appModel = BuildAppModel(builder =>
            builder.AddJavaApp("javaapp", "../java-project", "target/app.jar")
                   .WithJvmArgs("-Xmx512m"));

        var resource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());

        var args = await resource.GetArgumentListAsync();
        Assert.Equal("-Xmx512m", args[0]);
        Assert.Contains("-jar", args);
        Assert.Contains("target/app.jar", args);
    }

    [Fact]
    public async Task MavenBuildCreatesResourceWithCorrectArgs()
    {
        var appModel = BuildAppModel(builder =>
            builder.AddJavaApp("java", Environment.CurrentDirectory, "target/app.jar")
                   .WithMavenBuild());

        var javaResource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var buildResource = Assert.Single(appModel.Resources.OfType<MavenBuildResource>());

        Assert.Equal("java-maven-build", buildResource.Name);

        string expectedWrapper = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "mvnw.cmd" : "mvnw";
        Assert.Equal(Path.GetFullPath(Path.Combine(javaResource.WorkingDirectory, expectedWrapper)), buildResource.Command);

        Assert.True(javaResource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        Assert.Contains(waitAnnotations, w => w.Resource == buildResource);

        var args = await buildResource.GetArgumentListAsync();
        Assert.Equal(["clean", "package"], args);
    }

    [Fact]
    public async Task MavenBuildWithCustomArgsReplacesDefaults()
    {
        var appModel = BuildAppModel(builder =>
            builder.AddJavaApp("java", Environment.CurrentDirectory, "target/app.jar")
                   .WithMavenBuild(args: ["-DskipTests", "-Pprod"]));

        var buildResource = Assert.Single(appModel.Resources.OfType<MavenBuildResource>());

        var args = await buildResource.GetArgumentListAsync();
        Assert.Equal(["-DskipTests", "-Pprod"], args);
    }

    [Fact]
    public void MavenBuildWithCustomWrapperUsesProvidedPath()
    {
        var appModel = BuildAppModel(builder =>
            builder.AddJavaApp("java", Environment.CurrentDirectory, "target/app.jar")
                   .WithMavenBuild(wrapperScript: "custom/mvnw"));

        var javaResource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var buildResource = Assert.Single(appModel.Resources.OfType<MavenBuildResource>());
        Assert.Equal(Path.GetFullPath(Path.Combine(javaResource.WorkingDirectory, "custom/mvnw")), buildResource.Command);
    }

    [Fact]
    public async Task GradleBuildCreatesResourceWithCorrectArgs()
    {
        var appModel = BuildAppModel(builder =>
            builder.AddJavaApp("java", Environment.CurrentDirectory, "build/libs/app.jar")
                   .WithGradleBuild());

        var javaResource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var buildResource = Assert.Single(appModel.Resources.OfType<GradleBuildResource>());

        Assert.Equal("java-gradle-build", buildResource.Name);

        string expectedWrapper = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gradlew.bat" : "gradlew";
        Assert.Equal(Path.GetFullPath(Path.Combine(javaResource.WorkingDirectory, expectedWrapper)), buildResource.Command);

        Assert.True(javaResource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        Assert.Contains(waitAnnotations, w => w.Resource == buildResource);

        var args = await buildResource.GetArgumentListAsync();
        Assert.Equal(["clean", "build"], args);
    }

    [Fact]
    public async Task GradleBuildWithCustomArgsReplacesDefaults()
    {
        var appModel = BuildAppModel(builder =>
            builder.AddJavaApp("java", Environment.CurrentDirectory, "build/libs/app.jar")
                   .WithGradleBuild(args: ["-x", "test", "--parallel"]));

        var buildResource = Assert.Single(appModel.Resources.OfType<GradleBuildResource>());

        var args = await buildResource.GetArgumentListAsync();
        Assert.Equal(["-x", "test", "--parallel"], args);
    }

    [Fact]
    public void GradleBuildWithCustomWrapperUsesProvidedPath()
    {
        var appModel = BuildAppModel(builder =>
            builder.AddJavaApp("java", Environment.CurrentDirectory, "build/libs/app.jar")
                   .WithGradleBuild(wrapperScript: "custom/gradlew"));

        var javaResource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var buildResource = Assert.Single(appModel.Resources.OfType<GradleBuildResource>());
        Assert.Equal(Path.GetFullPath(Path.Combine(javaResource.WorkingDirectory, "custom/gradlew")), buildResource.Command);
    }

    [Fact]
    public async Task AddJavaAppWithOtelAgentSetsEnvironment()
    {
        var appModel = BuildAppModel(builder =>
            builder.AddJavaApp("java", Environment.CurrentDirectory, "target/app.jar")
                   .WithOtelAgent("/agents/opentelemetry-javaagent.jar"));

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

    private static DistributedApplicationModel BuildAppModel(Action<IDistributedApplicationBuilder> configure)
    {
        var builder = DistributedApplication.CreateBuilder();
        configure(builder);
        var app = builder.Build();
        return app.Services.GetRequiredService<DistributedApplicationModel>();
    }
}
