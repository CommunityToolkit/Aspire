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

    [Fact]
    public async Task AddJavaAppWithJvmArgs()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var options = new JavaAppExecutableResourceOptions
        {
            ApplicationName = "test.jar",
            JvmArgs = ["-Xmx512m", "-Darg=value"],
            Port = 8080
        };

        builder.AddJavaApp("java", Environment.CurrentDirectory, options);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<JavaAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("java", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out CommandLineArgsCallbackAnnotation? argsAnnotations));
        CommandLineArgsCallbackContext context = new([]);
        await argsAnnotations.Callback(context);
        
        // JVM args should come before -jar
        Assert.Contains("-Xmx512m", context.Args);
        Assert.Contains("-Darg=value", context.Args);
        Assert.Contains("-jar", context.Args);
        Assert.Contains(options.ApplicationName, context.Args);
        
        // Verify order: JVM args before -jar
        var xmxIndex = Array.IndexOf(context.Args, "-Xmx512m");
        var dargIndex = Array.IndexOf(context.Args, "-Darg=value");
        var jarIndex = Array.IndexOf(context.Args, "-jar");
        
        Assert.True(xmxIndex < jarIndex, "JVM arg -Xmx512m should come before -jar");
        Assert.True(dargIndex < jarIndex, "JVM arg -Darg=value should come before -jar");
    }

    [Fact]
    public async Task AddJavaAppWithJvmArgsAndApplicationArgs()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var options = new JavaAppExecutableResourceOptions
        {
            ApplicationName = "test.jar",
            JvmArgs = ["-Xmx512m", "-Darg=value"],
            Args = ["--app-arg1", "--app-arg2"],
            Port = 8080
        };

        builder.AddJavaApp("java", Environment.CurrentDirectory, options);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<JavaAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.True(resource.TryGetLastAnnotation(out CommandLineArgsCallbackAnnotation? argsAnnotations));
        CommandLineArgsCallbackContext context = new([]);
        await argsAnnotations.Callback(context);
        
        // All arguments should be present
        Assert.Contains("-Xmx512m", context.Args);
        Assert.Contains("-Darg=value", context.Args);
        Assert.Contains("-jar", context.Args);
        Assert.Contains(options.ApplicationName, context.Args);
        Assert.Contains("--app-arg1", context.Args);
        Assert.Contains("--app-arg2", context.Args);
        
        // Verify order: JVM args before -jar, application args after jar file
        var xmxIndex = Array.IndexOf(context.Args, "-Xmx512m");
        var dargIndex = Array.IndexOf(context.Args, "-Darg=value");
        var jarIndex = Array.IndexOf(context.Args, "-jar");
        var jarFileIndex = Array.IndexOf(context.Args, options.ApplicationName);
        var appArg1Index = Array.IndexOf(context.Args, "--app-arg1");
        var appArg2Index = Array.IndexOf(context.Args, "--app-arg2");
        
        Assert.True(xmxIndex < jarIndex, "JVM arg -Xmx512m should come before -jar");
        Assert.True(dargIndex < jarIndex, "JVM arg -Darg=value should come before -jar");
        Assert.True(jarIndex < jarFileIndex, "-jar should come before jar file name");
        Assert.True(jarFileIndex < appArg1Index, "Application args should come after jar file");
        Assert.True(jarFileIndex < appArg2Index, "Application args should come after jar file");
    }

    [Fact]
    public async Task AddJavaAppWithOnlyApplicationArgs()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var options = new JavaAppExecutableResourceOptions
        {
            ApplicationName = "test.jar",
            Args = ["--app-arg"],
            Port = 8080
        };

        builder.AddJavaApp("java", Environment.CurrentDirectory, options);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<JavaAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.True(resource.TryGetLastAnnotation(out CommandLineArgsCallbackAnnotation? argsAnnotations));
        CommandLineArgsCallbackContext context = new([]);
        await argsAnnotations.Callback(context);
        
        // Should have -jar, jar file, and app args
        Assert.Contains("-jar", context.Args);
        Assert.Contains(options.ApplicationName, context.Args);
        Assert.Contains("--app-arg", context.Args);
        
        // Verify order
        var jarIndex = Array.IndexOf(context.Args, "-jar");
        var jarFileIndex = Array.IndexOf(context.Args, options.ApplicationName);
        var appArgIndex = Array.IndexOf(context.Args, "--app-arg");
        
        Assert.True(jarIndex < jarFileIndex, "-jar should come before jar file name");
        Assert.True(jarFileIndex < appArgIndex, "Application args should come after jar file");
    }
}
