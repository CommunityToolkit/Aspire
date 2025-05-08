using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Hosting;
using Minio;
using Minio.DataModel.Args;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Minio.Tests;

[RequiresDocker]
public class MinioFunctionalTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task StorageGetsCreatedAndUsable()
    {
        using var distributedApplicationBuilder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        var rootUser = "minioadmin";
        var port = 9000;

        var passwordParameter = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder,
            $"rootPassword");
        distributedApplicationBuilder.Configuration["Parameters:rootPassword"] = passwordParameter.Value;
        var rootPasswordParameter = distributedApplicationBuilder.AddParameter(passwordParameter.Name);
        
        var minio = distributedApplicationBuilder
            .AddMinioContainer("minio",
                distributedApplicationBuilder.AddParameter("username", rootUser),
                rootPasswordParameter,
                minioPort: port);

        await using var app = await distributedApplicationBuilder.BuildAsync();
        
        await app.StartAsync();
        
        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceHealthyAsync(minio.Resource.Name);
        
        var webApplicationBuilder = Host.CreateApplicationBuilder();
        
        webApplicationBuilder.Services.AddMinio(configureClient => configureClient
            .WithEndpoint("localhost", port)
            .WithCredentials(rootUser, passwordParameter.Value)
            .WithSSL(false)
            .Build());
        
        using var host = webApplicationBuilder.Build();

        await host.StartAsync();

        var minioClient = host.Services.GetRequiredService<IMinioClient>();

        var bucketName = "somebucket";
        
        var mbArgs = new MakeBucketArgs()
            .WithBucket(bucketName);
        await minioClient.MakeBucketAsync(mbArgs);

        var res = await minioClient.ListBucketsAsync();

        Assert.NotEmpty(res.Buckets);

        var bytearr = "Hey, I'm using minio client! It's awesome!"u8.ToArray();
        var stream = new MemoryStream(bytearr);

        var objectName = "someobj";
        var contentType = "text/plain";
        
        var putObjectArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType);
        
        await minioClient.PutObjectAsync(putObjectArgs);
        
        var statObject = new StatObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);

        var meta = await minioClient.StatObjectAsync(statObject);
        
        Assert.NotNull(meta);
        Assert.Equal(contentType, meta.ContentType);
    }
}