using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;

namespace CommunityToolkit.Aspire.Hosting.Java.Tests;

public class PublishResourceCreationTests
{
    [Fact]
    public void AddJavaAppAddsManifestPublishingAnnotationInPublishMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var java = builder.AddJavaApp("java", Environment.CurrentDirectory, "target/app.jar");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var publishedResource = Assert.Single(appModel.Resources, resource => resource.Name == "java");
        Assert.Contains("ExecutableContainerResource", publishedResource.GetType().Name);

        Assert.True(java.Resource.TryGetAnnotationsOfType<ManifestPublishingCallbackAnnotation>(out var annotations));
        Assert.NotEmpty(annotations);
    }

    [Fact]
    public void MavenBuildUsesPublishAnnotationWithoutCreatingBuildResourceInPublishMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var java = builder.AddJavaApp("java", Environment.CurrentDirectory, "target/app.jar")
                          .WithMavenBuild();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Empty(appModel.Resources.OfType<MavenBuildResource>());
        Assert.False(java.Resource.TryGetAnnotationsOfType<WaitAnnotation>(out _));
        Assert.True(java.Resource.TryGetLastAnnotation<JavaPublishBuildAnnotation>(out var publishAnnotation));
        Assert.Equal(JavaBuildTool.Maven, publishAnnotation.Tool);
        Assert.Null(publishAnnotation.WrapperPath);
        Assert.Equal(["clean", "package"], publishAnnotation.Args);
    }

    [Fact]
    public void MavenGoalKeepsJavaCommandAndAddsPublishBuildInPublishMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var java = builder.AddJavaApp("java", Environment.CurrentDirectory)
                          .WithMavenGoal("spring-boot:run");

        using var app = builder.Build();

        Assert.Equal("java", java.Resource.Command);
        Assert.True(java.Resource.TryGetLastAnnotation<JavaBuildToolAnnotation>(out var runAnnotation));
        Assert.Equal(JavaBuildTool.Maven, runAnnotation.Tool);
        Assert.Equal(["spring-boot:run"], runAnnotation.Args);

        Assert.True(java.Resource.TryGetLastAnnotation<JavaPublishBuildAnnotation>(out var publishAnnotation));
        Assert.Equal(JavaBuildTool.Maven, publishAnnotation.Tool);
        Assert.Null(publishAnnotation.WrapperPath);
        Assert.Equal(["package"], publishAnnotation.Args);
    }

    [Fact]
    public void GradleTaskKeepsJavaCommandAndAddsPublishBuildInPublishMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var java = builder.AddJavaApp("java", Environment.CurrentDirectory)
                          .WithGradleTask("bootRun");

        using var app = builder.Build();

        Assert.Equal("java", java.Resource.Command);
        Assert.True(java.Resource.TryGetLastAnnotation<JavaBuildToolAnnotation>(out var runAnnotation));
        Assert.Equal(JavaBuildTool.Gradle, runAnnotation.Tool);
        Assert.Equal(["bootRun"], runAnnotation.Args);

        Assert.True(java.Resource.TryGetLastAnnotation<JavaPublishBuildAnnotation>(out var publishAnnotation));
        Assert.Equal(JavaBuildTool.Gradle, publishAnnotation.Tool);
        Assert.Null(publishAnnotation.WrapperPath);
        Assert.Equal(["build"], publishAnnotation.Args);
    }

    [Fact]
    public void CustomWrapperPathIsPreservedForPublishMetadata()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var java = builder.AddJavaApp("java", Environment.CurrentDirectory)
                          .WithWrapperPath("tools/mvnw")
                          .WithMavenGoal("spring-boot:run");

        using var app = builder.Build();

        string expectedWrapper = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "tools/mvnw"));

        Assert.True(java.Resource.TryGetLastAnnotation<JavaBuildToolAnnotation>(out var runAnnotation));
        Assert.Equal(expectedWrapper, runAnnotation.WrapperPath);

        Assert.True(java.Resource.TryGetLastAnnotation<JavaPublishBuildAnnotation>(out var publishAnnotation));
        Assert.Equal(expectedWrapper, publishAnnotation.WrapperPath);
    }
}
