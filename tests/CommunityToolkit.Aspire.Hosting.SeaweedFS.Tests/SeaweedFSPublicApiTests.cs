using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.SeaweedFS.Tests;

public class SeaweedFSPublicApiTests
{
    [Fact]
    public void AddSeaweedFS_ThrowsWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "seaweedfs";

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.AddSeaweedFS(name);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddSeaweedFS_ThrowsWhenNameIsNullOrEmpty(string? name)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.AddSeaweedFS(name!);

        ArgumentException exception = Assert.ThrowsAny<ArgumentException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void WithS3_ThrowsWhenBuilderIsNull()
    {
        IResourceBuilder<SeaweedFSContainerResource> builder = null!;

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithS3();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithFiler_ThrowsWhenBuilderIsNull()
    {
        IResourceBuilder<SeaweedFSContainerResource> builder = null!;

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithFiler();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithHostPort_ThrowsWhenBuilderIsNull()
    {
        IResourceBuilder<SeaweedFSContainerResource> builder = null!;

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithHostPort(9333);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithAccessKey_ThrowsWhenBuilderIsNull()
    {
        IResourceBuilder<SeaweedFSContainerResource> builder = null!;
        IDistributedApplicationBuilder builderApp = DistributedApplication.CreateBuilder();
        IResourceBuilder<ParameterResource> parameter = builderApp.AddParameter("test");

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithAccessKey(parameter);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithAccessKey_ThrowsWhenAccessKeyIsNull()
    {
        IDistributedApplicationBuilder builderApp = DistributedApplication.CreateBuilder();
        IResourceBuilder<SeaweedFSContainerResource> builder = builderApp.AddSeaweedFS("seaweedfs");

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithAccessKey(null!);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal("accessKey", exception.ParamName);
    }

    [Fact]
    public void WithSecretKey_ThrowsWhenBuilderIsNull()
    {
        IResourceBuilder<SeaweedFSContainerResource> builder = null!;
        IDistributedApplicationBuilder builderApp = DistributedApplication.CreateBuilder();
        IResourceBuilder<ParameterResource> parameter = builderApp.AddParameter("test");

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithSecretKey(parameter);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithSecretKey_ThrowsWhenSecretKeyIsNull()
    {
        IDistributedApplicationBuilder builderApp = DistributedApplication.CreateBuilder();
        IResourceBuilder<SeaweedFSContainerResource> builder = builderApp.AddSeaweedFS("seaweedfs");

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithSecretKey(null!);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal("secretKey", exception.ParamName);
    }

    [Fact]
    public void WithS3ConfigFile_ThrowsWhenBuilderIsNull()
    {
        IResourceBuilder<SeaweedFSContainerResource> builder = null!;

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithS3ConfigFile("config.json");

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void WithS3ConfigFile_ThrowsWhenConfigFilePathIsNullOrWhitespace(string? configFilePath)
    {
        IDistributedApplicationBuilder builderApp = DistributedApplication.CreateBuilder();
        IResourceBuilder<SeaweedFSContainerResource> builder = builderApp.AddSeaweedFS("seaweedfs");

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithS3ConfigFile(configFilePath!);

        ArgumentException exception = Assert.ThrowsAny<ArgumentException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal(nameof(configFilePath), exception.ParamName);
    }

    [Fact]
    public void WithDataBindMount_ThrowsWhenBuilderIsNull()
    {
        IResourceBuilder<SeaweedFSContainerResource> builder = null!;

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithDataBindMount("/data");

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithDataBindMount_ThrowsWhenSourceIsNull()
    {
        IDistributedApplicationBuilder builderApp = DistributedApplication.CreateBuilder();
        IResourceBuilder<SeaweedFSContainerResource> builder = builderApp.AddSeaweedFS("seaweedfs");

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithDataBindMount(null!);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void WithDataVolume_ThrowsWhenBuilderIsNull()
    {
        IResourceBuilder<SeaweedFSContainerResource> builder = null!;

        IResourceBuilder<SeaweedFSContainerResource> action() => builder.WithDataVolume();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>((Func<IResourceBuilder<SeaweedFSContainerResource>>)action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void VerifySeaweedFSContainerResource_WithHostPort()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddSeaweedFS("seaweed")
            .WithHostPort(1000);

        SeaweedFSContainerResource resource = Assert.Single(builder.Resources.OfType<SeaweedFSContainerResource>());
        EndpointAnnotation endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>(), x => x.Name == SeaweedFSContainerResource.MasterEndpointName);

        Assert.Equal(1000, endpoint.Port);
    }

    [Fact]
    public async Task VerifyConnectionString_PointsToMaster_WhenS3IsNotEnabled()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<SeaweedFSContainerResource> seaweed = builder.AddSeaweedFS("seaweed")
            .WithEndpoint(SeaweedFSContainerResource.MasterEndpointName, e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 9333));

        string? connectionString = await seaweed.Resource.GetConnectionStringAsync();

        Assert.Equal("Endpoint=http://localhost:9333", connectionString);
    }

    [Fact]
    public async Task VerifyConnectionString_PointsToS3AndIncludesCredentials_WhenS3IsEnabled()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ParameterResource> accessKey = builder.AddParameter("access", "my-access");
        IResourceBuilder<ParameterResource> secretKey = builder.AddParameter("secret", "my-secret");

        IResourceBuilder<SeaweedFSContainerResource> seaweed = builder.AddSeaweedFS("seaweed")
            .WithAccessKey(accessKey)
            .WithSecretKey(secretKey)
            .WithS3()
            // Mock the S3 endpoint allocation
            .WithEndpoint(SeaweedFSContainerResource.S3EndpointName, e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 8333))
            // Mock the Filer endpoint allocation (implicitly added by WithS3) to prevent async evaluation hangs
            .WithEndpoint(SeaweedFSContainerResource.FilerEndpointName, e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 8888));

        string? connectionString = await seaweed.Resource.GetConnectionStringAsync();

        // Assert dynamically against the reference expression to evaluate parameters just like the real AppHost
        // Now includes the FilerEndpoint appended dynamically by the Resource
        string? expected = await ReferenceExpression.Create($"Endpoint=http://localhost:8333;AccessKey={accessKey.Resource};SecretKey={secretKey.Resource};FilerEndpoint=http://localhost:8888").GetValueAsync(CancellationToken.None);

        Assert.Equal(expected, connectionString);
    }
}