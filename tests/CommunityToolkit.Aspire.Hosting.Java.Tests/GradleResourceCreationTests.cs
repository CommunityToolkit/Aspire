using System.Runtime.InteropServices;
using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Java.Tests;

public class GradleResourceCreationTests
{
    [Fact]
    public void AddingGradleBuildCreatesResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("java", Environment.CurrentDirectory)
            .WithGradleBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var javaResource = Assert.Single(appModel.Resources.OfType<JavaAppExecutableResource>());
        var buildResource = Assert.Single(appModel.Resources.OfType<GradleBuildResource>());

        Assert.Equal("java-gradle-build", buildResource.Name);
        Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : Path.Combine(Environment.CurrentDirectory, "gradlew"), buildResource.Command);
        
        Assert.True(javaResource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        Assert.Contains(waitAnnotations, w => w.Resource == buildResource);
    }

    [Fact]
    public async Task AddingGradleBuildWithOverrides()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddJavaApp("java", Environment.CurrentDirectory)
            .WithGradleBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var buildResource = Assert.Single(appModel.Resources.OfType<GradleBuildResource>());

        var args = await buildResource.GetArgumentListAsync();
        string[] expectedArgs = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ["/c", "gradlew.bat", "clean", "build"] : ["clean", "build"];
        Assert.Equal(expectedArgs, args);
    }
}
