using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.SeaweedFS.Client.Tests;

public class SeaweedFSClientTests
{
    [Fact]
    public void AddSeaweedFSS3Client_ThrowsArgumentNullException_WhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        void action() => builder.AddSeaweedFSS3Client("seaweedfs");

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddSeaweedFSS3Client_ThrowsArgumentException_WhenConnectionNameIsNull()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        void action() => builder.AddSeaweedFSS3Client(null!);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal("connectionName", exception.ParamName);
    }

    [Fact]
    public void AddSeaweedFSS3Client_ThrowsInvalidOperationException_WhenEndpointIsMissing()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        void action()
        {
            builder.AddSeaweedFSS3Client("seaweedfs");

            using IHost host = builder.Build();
            _ = host.Services.GetRequiredKeyedService<IAmazonS3>("seaweedfs");
        }

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("A valid absolute SeaweedFS endpoint URI must be provided", exception.Message);
    }

    [Fact]
    public void AddSeaweedFSS3Client_RegistersKeyedAndStandardServices()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:seaweedfs", "Endpoint=http://localhost:8333" },
            { "ConnectionStrings:cluster2", "Endpoint=http://localhost:9333" }
        });

        builder.AddSeaweedFSS3Client("seaweedfs");
        builder.AddSeaweedFSS3Client("cluster2");

        using IHost host = builder.Build();

        IAmazonS3 client1 = host.Services.GetRequiredKeyedService<IAmazonS3>("seaweedfs");
        IAmazonS3 client2 = host.Services.GetRequiredKeyedService<IAmazonS3>("cluster2");

        Assert.NotNull(client1);
        Assert.NotNull(client2);
        Assert.NotSame(client1, client2);

        IAmazonS3 defaultClient = host.Services.GetRequiredService<IAmazonS3>();
        Assert.NotNull(defaultClient);
        Assert.Same(client1, defaultClient);
    }

    [Fact]
    public void AddSeaweedFSS3Client_ConfiguresS3ClientCorrectly()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:seaweedfs", "Endpoint=http://localhost:8333" }
        });

        builder.AddSeaweedFSS3Client("seaweedfs");

        using IHost host = builder.Build();
        IAmazonS3 client = host.Services.GetRequiredKeyedService<IAmazonS3>("seaweedfs");

        AmazonS3Config s3Config = Assert.IsType<AmazonS3Config>(client.Config);

        Assert.Equal("http://localhost:8333/", s3Config.ServiceURL);
        Assert.True(s3Config.ForcePathStyle);
        Assert.True(s3Config.UseHttp);
    }

    [Fact]
    public void AddSeaweedFSS3Client_OverridesSchemeWithUseSsl()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Even if HTTP is used in the string, UseSsl=true should override it to HTTPS
            { "ConnectionStrings:seaweedfs", "Endpoint=http://localhost:8333;UseSsl=true" }
        });

        builder.AddSeaweedFSS3Client("seaweedfs");

        using IHost host = builder.Build();
        IAmazonS3 client = host.Services.GetRequiredKeyedService<IAmazonS3>("seaweedfs");

        AmazonS3Config s3Config = Assert.IsType<AmazonS3Config>(client.Config);

        Assert.Equal("https://localhost:8333/", s3Config.ServiceURL);
        Assert.False(s3Config.UseHttp); // S3 uses HTTPS natively
    }

    [Fact]
    public void AddSeaweedFSS3Client_InjectsAnonymousCredentials_WhenKeysAreMissing()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:seaweedfs", "Endpoint=http://localhost:8333" }
        });

        builder.AddSeaweedFSS3Client("seaweedfs");

        using IHost host = builder.Build();
        AmazonS3Client client = (AmazonS3Client)host.Services.GetRequiredService<IAmazonS3>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddSeaweedFSS3Client_AppliesProvidedCredentials_FromConnectionString()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:seaweedfs", "Endpoint=http://localhost:8333;AccessKey=my-admin;SecretKey=my-secret" }
        });

        builder.AddSeaweedFSS3Client("seaweedfs");

        using IHost host = builder.Build();
        AmazonS3Client client = (AmazonS3Client)host.Services.GetRequiredService<IAmazonS3>();

        Assert.NotNull(client);
    }
}