using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.SeaweedFS.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void AddSeaweedFS_CreatesResourceWithDefaultEndpoints()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddSeaweedFS("seaweed");

        using DistributedApplication app = builder.Build();

        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        SeaweedFSContainerResource resource = Assert.Single(appModel.Resources.OfType<SeaweedFSContainerResource>());

        Assert.Equal("seaweed", resource.Name);

        // Assert that only Master and Volume endpoints are created by default
        EndpointAnnotation? masterEndpoint = resource.Annotations.OfType<EndpointAnnotation>().SingleOrDefault(e => e.Name == SeaweedFSContainerResource.MasterEndpointName);
        EndpointAnnotation? volumeEndpoint = resource.Annotations.OfType<EndpointAnnotation>().SingleOrDefault(e => e.Name == SeaweedFSContainerResource.VolumeEndpointName);

        Assert.NotNull(masterEndpoint);
        Assert.Equal(9333, masterEndpoint.TargetPort);

        Assert.NotNull(volumeEndpoint);
        Assert.Equal(8080, volumeEndpoint.TargetPort);

        // Assert that Opt-In endpoints are not present
        Assert.Null(resource.Annotations.OfType<EndpointAnnotation>().SingleOrDefault(e => e.Name == SeaweedFSContainerResource.FilerEndpointName));
        Assert.Null(resource.Annotations.OfType<EndpointAnnotation>().SingleOrDefault(e => e.Name == SeaweedFSContainerResource.S3EndpointName));
    }

    [Fact]
    public void AddSeaweedFS_HasHealthCheck()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddSeaweedFS("seaweed");

        using DistributedApplication app = builder.Build();

        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        SeaweedFSContainerResource? resource = appModel.Resources.OfType<SeaweedFSContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);

        bool result = resource.TryGetAnnotationsOfType<HealthCheckAnnotation>(out IEnumerable<HealthCheckAnnotation>? annotations);

        Assert.True(result);
        Assert.NotNull(annotations);
        Assert.Single(annotations);
    }

    [Fact]
    public void WithFiler_AddsFilerEndpointAndAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddSeaweedFS("seaweed").WithFiler();

        using DistributedApplication app = builder.Build();

        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        SeaweedFSContainerResource resource = appModel.Resources.OfType<SeaweedFSContainerResource>().Single();

        EndpointAnnotation? filerEndpoint = resource.Annotations.OfType<EndpointAnnotation>().SingleOrDefault(e => e.Name == SeaweedFSContainerResource.FilerEndpointName);

        Assert.NotNull(filerEndpoint);
        Assert.Equal(8888, filerEndpoint.TargetPort);

        Assert.Contains(resource.Annotations, a => a is SeaweedFSFilerAnnotation);
    }

    [Fact]
    public void WithS3_AddsS3EndpointAndAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddSeaweedFS("seaweed").WithS3();

        using DistributedApplication app = builder.Build();

        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        SeaweedFSContainerResource resource = appModel.Resources.OfType<SeaweedFSContainerResource>().Single();

        EndpointAnnotation? s3Endpoint = resource.Annotations.OfType<EndpointAnnotation>().SingleOrDefault(e => e.Name == SeaweedFSContainerResource.S3EndpointName);

        Assert.NotNull(s3Endpoint);
        Assert.Equal(8333, s3Endpoint.TargetPort);

        Assert.Contains(resource.Annotations, a => a is SeaweedFSS3Annotation);
    }

    [Fact]
    public void WithDataVolume_AddsVolumeAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddSeaweedFS("seaweed").WithDataVolume();

        using DistributedApplication app = builder.Build();

        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        SeaweedFSContainerResource resource = appModel.Resources.OfType<SeaweedFSContainerResource>().Single();

        ContainerMountAnnotation? volumeAnnotation = resource.Annotations.OfType<ContainerMountAnnotation>().SingleOrDefault(a => a.Target == "/data");

        Assert.NotNull(volumeAnnotation);
        Assert.Equal(ContainerMountType.Volume, volumeAnnotation.Type);
        Assert.False(string.IsNullOrWhiteSpace(volumeAnnotation.Source));
    }
}