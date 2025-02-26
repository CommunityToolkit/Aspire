using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.LavinMQ.Tests;

public class ContainerResourceCreationTests
{
    [Fact]
    public void AddLavinMqApiBuilderBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddLavinMQ("lavinmq"));
    }

    [Fact]
    public void AddLavinMqApiBuilderNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddLavinMQ(null!));
    }

    [Fact]
    public void AddLavinMqApiBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddLavinMQ("lavinmq");
        
        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        LavinMQContainerResource? resource = appModel.Resources.OfType<LavinMQContainerResource>().SingleOrDefault();
        
        Assert.NotNull(resource);
        Assert.Equal("lavinmq", resource.Name);
        ValidateLavinMqContainerImageAnnotations(resource);
        ValidateEndpointAnnotations(resource, LavinMQContainerResource.DefaultAmqpPort, LavinMQContainerResource.DefaultManagementPort);
    }
    
    [Fact]
    public void AddLavinMqApiBuilderContainerDetailsSetOnResourceCustomPorts()
    {
        const int amqpPort = 1111;
        const int managementPort = 2222;
        
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddLavinMQ("lavinmq", amqpPort, managementPort);
        
        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        LavinMQContainerResource? resource = appModel.Resources.OfType<LavinMQContainerResource>().SingleOrDefault();
        
        Assert.NotNull(resource);
        Assert.Equal("lavinmq", resource.Name);
        ValidateLavinMqContainerImageAnnotations(resource);
        ValidateEndpointAnnotations(resource, amqpPort, managementPort);
    }

    private static void ValidateLavinMqContainerImageAnnotations(LavinMQContainerResource resource)
    {
        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal("2.1.0", imageAnnotations.Tag);
        Assert.Equal("cloudamqp/lavinmq", imageAnnotations.Image);
        Assert.Equal("docker.io", imageAnnotations.Registry);
    }

    private static void ValidateEndpointAnnotations(LavinMQContainerResource resource, int amqpPort, int managementPort)
    {
        resource.TryGetAnnotationsOfType<EndpointAnnotation>(out IEnumerable<EndpointAnnotation>? annotations);
        Assert.NotNull(annotations);
        
        List<EndpointAnnotation> endpointAnnotations = annotations.ToList();
        
        Assert.Equal(2, endpointAnnotations.Count);
        
        Assert.Equal(LavinMQContainerResource.PrimaryEndpointSchema, endpointAnnotations[0].UriScheme);
        Assert.Equal(LavinMQContainerResource.PrimaryEndpointName, endpointAnnotations[0].Name);
        Assert.Equal(amqpPort, endpointAnnotations[0].Port);
        
        Assert.Equal(LavinMQContainerResource.ManagementEndpointSchema, endpointAnnotations[1].UriScheme);
        Assert.Equal(LavinMQContainerResource.ManagementEndpointName, endpointAnnotations[1].Name);
        Assert.Equal(managementPort, endpointAnnotations[1].Port);
    }
}
