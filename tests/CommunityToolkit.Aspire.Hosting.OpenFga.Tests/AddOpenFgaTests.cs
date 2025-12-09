using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.OpenFga.Tests;

public class AddOpenFgaTests
{
    [Fact]
    public void AddOpenFgaAddsResourceWithDefaultConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        builder.AddOpenFga("openfga");

        using var app = builder.Build();
        
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<OpenFgaResource>());
        
        Assert.Equal("openfga", resource.Name);
    }

    [Fact]
    public void AddOpenFgaAddsContainerWithCorrectImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        builder.AddOpenFga("openfga");

        using var app = builder.Build();
        
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<OpenFgaResource>());
        
        Assert.True(resource.TryGetContainerImageName(out var imageName));
        Assert.Equal("docker.io/openfga/openfga:v1.8.5", imageName);
    }

    [Fact]
    public void AddOpenFgaAddsHttpEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        builder.AddOpenFga("openfga");

        using var app = builder.Build();
        
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<OpenFgaResource>());
        
        var httpEndpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .FirstOrDefault(e => e.Name == "http");
        Assert.NotNull(httpEndpoint);
        Assert.Equal(8080, httpEndpoint.TargetPort);
    }

    [Fact]
    public void AddOpenFgaAddsGrpcEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        builder.AddOpenFga("openfga");

        using var app = builder.Build();
        
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<OpenFgaResource>());
        
        var grpcEndpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .FirstOrDefault(e => e.Name == "grpc");
        Assert.NotNull(grpcEndpoint);
        Assert.Equal(8081, grpcEndpoint.TargetPort);
    }

    [Fact]
    public void AddOpenFgaWithInMemoryDatastoreAddsEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        builder.AddOpenFga("openfga")
            .WithInMemoryDatastore();

        using var app = builder.Build();
        
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<OpenFgaResource>());
        
        var envAnnotation = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().FirstOrDefault();
        Assert.NotNull(envAnnotation);
    }

    [Fact]
    public void AddOpenFgaWithDataVolumeAddsVolume()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        builder.AddOpenFga("openfga")
            .WithDataVolume();

        using var app = builder.Build();
        
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<OpenFgaResource>());
        
        var mount = resource.Annotations.OfType<ContainerMountAnnotation>()
            .FirstOrDefault(m => m.Target == "/data");
        Assert.NotNull(mount);
    }

    [Fact]
    public void AddOpenFgaWithExperimentalFeaturesAddsEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        builder.AddOpenFga("openfga")
            .WithExperimentalFeatures("enable-list-users");

        using var app = builder.Build();
        
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<OpenFgaResource>());
        
        var envAnnotation = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().FirstOrDefault();
        Assert.NotNull(envAnnotation);
    }

    [Fact]
    public void AddOpenFgaHasConnectionStringExpression()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        builder.AddOpenFga("openfga");

        using var app = builder.Build();
        
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<OpenFgaResource>());
        
        var connectionStringExpression = resource.ConnectionStringExpression;
        
        Assert.NotNull(connectionStringExpression);
    }

    [Fact]
    public void AddOpenFgaWithCustomPortsUsesSpecifiedPorts()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        builder.AddOpenFga("openfga", httpPort: 9000, grpcPort: 9001);

        using var app = builder.Build();
        
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<OpenFgaResource>());
        
        var httpEndpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .FirstOrDefault(e => e.Name == "http");
        Assert.NotNull(httpEndpoint);
        Assert.Equal(9000, httpEndpoint.Port);
        
        var grpcEndpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .FirstOrDefault(e => e.Name == "grpc");
        Assert.NotNull(grpcEndpoint);
        Assert.Equal(9001, grpcEndpoint.Port);
    }

    [Fact]
    public void AddOpenFgaThrowsWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddOpenFga("openfga"));
    }

    [Fact]
    public void AddOpenFgaThrowsWhenNameIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddOpenFga(null!));
    }

    [Fact]
    public void AddOpenFgaThrowsWhenNameIsEmpty()
    {
        var builder = DistributedApplication.CreateBuilder();

#pragma warning disable ASPIRE006 // Testing empty name validation
        Assert.Throws<ArgumentException>(() => builder.AddOpenFga(""));
#pragma warning restore ASPIRE006
    }
}
