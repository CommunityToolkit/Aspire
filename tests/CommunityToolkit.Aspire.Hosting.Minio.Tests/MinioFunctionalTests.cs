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

        var passwordParameter = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(distributedApplicationBuilder,
            $"rootPassword");
        distributedApplicationBuilder.Configuration["Parameters:rootPassword"] = await passwordParameter.GetValueAsync(default);
        var rootPasswordParameter = distributedApplicationBuilder.AddParameter(passwordParameter.Name);
        
        var minio = distributedApplicationBuilder
            .AddMinioContainer("minio",
                distributedApplicationBuilder.AddParameter("username", rootUser),
                rootPasswordParameter);

        var minioEndpoint = minio.GetEndpoint("http");

        await using var app = await distributedApplicationBuilder.BuildAsync();
        
        await app.StartAsync();
        
        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceHealthyAsync(minio.Resource.Name);
        
        var webApplicationBuilder = Host.CreateApplicationBuilder();
        
        webApplicationBuilder.Services.AddMinio(async configureClient => configureClient
            .WithEndpoint("localhost", minioEndpoint.Port)
            .WithCredentials(rootUser, await passwordParameter.GetValueAsync(default))
            .WithSSL(false)
            .Build());
        
        using var host = webApplicationBuilder.Build();

        await host.StartAsync();

        var minioClient = host.Services.GetRequiredService<IMinioClient>();

        await TestApi(minioClient);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithDataShouldPersistStateBetweenUsages(bool useVolume)
    {
        string? volumeName = null;
        string? bindMountPath = null;

        try
        {
            using var builder1 = TestDistributedApplicationBuilder.Create(testOutputHelper);

            var rootUser = "minioadmin";

            var passwordParameter = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder1,
                $"rootPassword");
            builder1.Configuration["Parameters:rootPassword"] = await passwordParameter.GetValueAsync(default);
            var rootPasswordParameter = builder1.AddParameter(passwordParameter.Name);
            
            var minio1 = builder1.AddMinioContainer("minio",
                builder1.AddParameter("username", rootUser),
                rootPasswordParameter);

            var minio1Endpoint = minio1.GetEndpoint("http");
            
            if (useVolume)
            {
                // Use a deterministic volume name to prevent them from exhausting the machines if deletion fails
                volumeName = VolumeNameGenerator.Generate(minio1, nameof(WithDataShouldPersistStateBetweenUsages));

                // if the volume already exists (because of a crashing previous run), delete it
                DockerUtils.AttemptDeleteDockerVolume(volumeName, throwOnFailure: true);
                minio1.WithDataVolume(volumeName);
            }
            else
            {
                bindMountPath = Directory.CreateTempSubdirectory().FullName;
                minio1.WithDataBindMount(bindMountPath);
            }

            using (var app = builder1.Build())
            {
                await app.StartAsync();

                var rns = app.Services.GetRequiredService<ResourceNotificationService>();

                await rns.WaitForResourceHealthyAsync(minio1.Resource.Name);
                
                try
                {
                    var webApplicationBuilder = Host.CreateApplicationBuilder();
        
                    webApplicationBuilder.Services.AddMinio(async configureClient => configureClient
                        .WithEndpoint("localhost", minio1Endpoint.Port)
                        .WithCredentials(rootUser, await passwordParameter.GetValueAsync(default))
                        .WithSSL(false)
                        .Build());
        
                    using var host = webApplicationBuilder.Build();

                    await host.StartAsync();

                    var minioClient = host.Services.GetRequiredService<IMinioClient>();
                    await TestApi(minioClient);
                }
                finally
                {
                    // Stops the container, or the Volume would still be in use
                    await app.StopAsync();
                }
            }
            
            using var builder2 = TestDistributedApplicationBuilder.Create(testOutputHelper);
            builder2.Configuration["Parameters:rootPassword"] = await passwordParameter.GetValueAsync(default);
            var rootPasswordParameter2 = builder2.AddParameter(passwordParameter.Name);
            
            
            var minio2 = builder2.AddMinioContainer("minio",
                builder2.AddParameter("username", rootUser),
                rootPasswordParameter2);
            
            var minio2Endpoint = minio2.GetEndpoint("http");

            if (useVolume)
            {
                minio2.WithDataVolume(volumeName);
            }
            else
            {
                minio2.WithDataBindMount(bindMountPath!);
            }

            using (var app = builder2.Build())
            {
                await app.StartAsync();

                var rns = app.Services.GetRequiredService<ResourceNotificationService>();

                await rns.WaitForResourceHealthyAsync(minio1.Resource.Name);
                
                
                try
                {
                    var webApplicationBuilder = Host.CreateApplicationBuilder();
        
                    webApplicationBuilder.Services.AddMinio(async configureClient => configureClient
                        .WithEndpoint("localhost", minio2Endpoint.Port)
                        .WithCredentials(rootUser, await passwordParameter.GetValueAsync(default))
                        .WithSSL(false)
                        .Build());
        
                    using var host = webApplicationBuilder.Build();

                    await host.StartAsync();

                    var minioClient = host.Services.GetRequiredService<IMinioClient>();
                    await TestApi(minioClient, isDataPreGenerated: false);
                }
                finally
                {
                    // Stops the container, or the Volume would still be in use
                    await app.StopAsync();
                }
            }

        }
        finally
        {
            if (volumeName is not null)
            {
                DockerUtils.AttemptDeleteDockerVolume(volumeName);
            }

            if (bindMountPath is not null)
            {
                try
                {
                    Directory.Delete(bindMountPath, recursive: true);
                }
                catch
                {
                    // Don't fail test if we can't clean the temporary folder
                }
            }
        }
    }

    private static async Task TestApi(IMinioClient minioClient, bool isDataPreGenerated = true)
    {
        const string bucketName = "somebucket";

        const string objectName = "someobj";
        const string contentType = "text/plain";
        
        if (isDataPreGenerated)
        {
            var mbArgs = new MakeBucketArgs()
                .WithBucket(bucketName);
            await minioClient.MakeBucketAsync(mbArgs);

            var res = await minioClient.ListBucketsAsync();

            Assert.NotEmpty(res.Buckets);

            var bytearr = "Hey, I'm using minio client! It's awesome!"u8.ToArray();
            var stream = new MemoryStream(bytearr);
        
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(contentType);
        
            await minioClient.PutObjectAsync(putObjectArgs);
        }
        
        var statObject = new StatObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);

        var meta = await minioClient.StatObjectAsync(statObject);
        
        Assert.NotNull(meta);
        Assert.Equal(contentType, meta.ContentType);
    }
}