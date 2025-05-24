using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Minio.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void MinioResourceGetsAdded()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddMinioContainer("minio");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<MinioContainerResource>());

        Assert.Equal("minio", resource.Name);
    }
    
    [Fact]
    public void MinioResourceHasHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddMinioContainer("minio");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<MinioContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("minio", resource.Name);

        var result = resource.TryGetAnnotationsOfType<HealthCheckAnnotation>(out var annotations);

        Assert.True(result);
        Assert.NotNull(annotations);

        Assert.Single(annotations);
    }
}