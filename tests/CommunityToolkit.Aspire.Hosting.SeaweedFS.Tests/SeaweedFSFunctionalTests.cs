using Amazon.S3;
using Amazon.S3.Model;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using System.Text;

namespace CommunityToolkit.Aspire.Hosting.SeaweedFS.Tests;

[RequiresDocker]
public class SeaweedFSFunctionalTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task S3Api_GetsCreatedAndUsable()
    {
        using IDistributedApplicationTestingBuilder builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        IResourceBuilder<SeaweedFSContainerResource> seaweedfs = builder.AddSeaweedFS("seaweedfs").WithS3();

        await using DistributedApplication app = await builder.BuildAsync();
        await app.StartAsync();

        ResourceNotificationService rns = app.Services.GetRequiredService<ResourceNotificationService>();
        await rns.WaitForResourceHealthyAsync(seaweedfs.Resource.Name);

        EndpointReference s3Endpoint = seaweedfs.GetEndpoint(SeaweedFSContainerResource.S3EndpointName);
        string? accessKey = await seaweedfs.Resource.AccessKey.GetValueAsync(default);
        string? secretKey = await seaweedfs.Resource.SecretKey.GetValueAsync(default);

        Assert.NotNull(accessKey);
        Assert.NotNull(secretKey);

        AmazonS3Config s3Config = new()
        {
            ServiceURL = s3Endpoint.Url,
            ForcePathStyle = true,
            UseHttp = true,
            Timeout = TimeSpan.FromSeconds(5),
            MaxErrorRetry = 0
        };

        using AmazonS3Client s3Client = new(accessKey, secretKey, s3Config);

        const string bucketName = "test-bucket";
        const string objectKey = "test-file.txt";
        const string fileContent = "Hello from SeaweedFS S3 API!";

        await ExecuteWithRetryAsync(async () =>
        {
            PutBucketResponse putBucketResponse = await s3Client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = bucketName
            });
            Assert.Equal(HttpStatusCode.OK, putBucketResponse.HttpStatusCode);

            PutObjectResponse putObjectResponse = await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                ContentBody = fileContent
            });
            Assert.Equal(HttpStatusCode.OK, putObjectResponse.HttpStatusCode);
        });

        GetObjectResponse getObjectResponse = await s3Client.GetObjectAsync(bucketName, objectKey);
        using StreamReader reader = new(getObjectResponse.ResponseStream);
        string downloadedContent = await reader.ReadToEndAsync();

        Assert.Equal(fileContent, downloadedContent);
    }

    [Fact]
    public async Task FilerApi_GetsCreatedAndUsable()
    {
        using IDistributedApplicationTestingBuilder builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        IResourceBuilder<SeaweedFSContainerResource> seaweedfs = builder.AddSeaweedFS("seaweedfs").WithFiler();

        await using DistributedApplication app = await builder.BuildAsync();
        await app.StartAsync();

        ResourceNotificationService rns = app.Services.GetRequiredService<ResourceNotificationService>();
        await rns.WaitForResourceHealthyAsync(seaweedfs.Resource.Name);

        EndpointReference filerEndpoint = seaweedfs.GetEndpoint(SeaweedFSContainerResource.FilerEndpointName);

        using HttpClient httpClient = new() { BaseAddress = new Uri(filerEndpoint.Url) };

        const string fileName = "/my-native-file.txt";
        const string fileContent = "Hello from SeaweedFS Native Filer API!";

        await ExecuteWithRetryAsync(async () =>
        {
            using StringContent content = new(fileContent, Encoding.UTF8, "text/plain");
            HttpResponseMessage response = await httpClient.PutAsync(fileName, content);
            response.EnsureSuccessStatusCode();
        });

        string downloadedContent = await GetStringWithRetryAsync(httpClient, fileName);

        Assert.Equal(fileContent, downloadedContent);
    }

    [Theory]
    [InlineData(true)] // Test with Docker Volume
    [InlineData(false)] // Test with Bind Mount
    public async Task WithData_ShouldPersistStateBetweenUsages(bool useVolume)
    {
        string? volumeName = null;
        string? bindMountPath = null;
        const string fileName = "/persisted-file.txt";
        const string fileContent = "This data should survive a restart.";

        try
        {
            // --- First run: Write data ---
            using (IDistributedApplicationTestingBuilder builder1 = TestDistributedApplicationBuilder.Create(testOutputHelper))
            {
                IResourceBuilder<SeaweedFSContainerResource> seaweed1 = builder1.AddSeaweedFS("seaweedfs").WithFiler();

                if (useVolume)
                {
                    volumeName = VolumeNameGenerator.Generate(seaweed1, nameof(WithData_ShouldPersistStateBetweenUsages));
                    DockerUtils.AttemptDeleteDockerVolume(volumeName, throwOnFailure: false);
                    seaweed1.WithDataVolume(volumeName);
                }
                else
                {
                    bindMountPath = Directory.CreateTempSubdirectory().FullName;
                    seaweed1.WithDataBindMount(bindMountPath);
                }

                using DistributedApplication app1 = builder1.Build();
                await app1.StartAsync();

                ResourceNotificationService rns1 = app1.Services.GetRequiredService<ResourceNotificationService>();
                await rns1.WaitForResourceHealthyAsync(seaweed1.Resource.Name);

                EndpointReference filerEndpoint1 = seaweed1.GetEndpoint(SeaweedFSContainerResource.FilerEndpointName);
                using HttpClient httpClient1 = new() { BaseAddress = new Uri(filerEndpoint1.Url) };

                await ExecuteWithRetryAsync(async () =>
                {
                    using StringContent content = new(fileContent, Encoding.UTF8, "text/plain");
                    HttpResponseMessage response = await httpClient1.PutAsync(fileName, content);
                    response.EnsureSuccessStatusCode();
                });

                await app1.StopAsync(); // Stops the container and flushes the data safely
            }

            // --- Second run: Read data ---
            using IDistributedApplicationTestingBuilder builder2 = TestDistributedApplicationBuilder.Create(testOutputHelper);
            IResourceBuilder<SeaweedFSContainerResource> seaweed2 = builder2.AddSeaweedFS("seaweedfs").WithFiler();

            if (useVolume)
            {
                seaweed2.WithDataVolume(volumeName);
            }
            else
            {
                seaweed2.WithDataBindMount(bindMountPath!);
            }

            using DistributedApplication app2 = builder2.Build();
            await app2.StartAsync();

            ResourceNotificationService rns2 = app2.Services.GetRequiredService<ResourceNotificationService>();
            await rns2.WaitForResourceHealthyAsync(seaweed2.Resource.Name);

            EndpointReference filerEndpoint2 = seaweed2.GetEndpoint(SeaweedFSContainerResource.FilerEndpointName);
            using HttpClient httpClient2 = new() { BaseAddress = new Uri(filerEndpoint2.Url) };

            string downloadedContent = await GetStringWithRetryAsync(httpClient2, fileName);

            Assert.Equal(fileContent, downloadedContent);

            await app2.StopAsync();
        }
        finally
        {
            // Cleanup cloud-native engine resources
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
                    // Ignore cleanup errors in pipeline tests
                }
            }
        }
    }

    /// <summary>
    /// Helper to retry actions, mitigating internal eventual consistency delays 
    /// in SeaweedFS cluster topology (Volume mapping to Master) just after boot.
    /// </summary>
    private static async Task ExecuteWithRetryAsync(Func<Task> action, int maxRetries = 15)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception)
            {
                if (i == maxRetries - 1)
                {
                    throw;
                }
                await Task.Delay(1000); // Wait 1 second before retrying
            }
        }
    }

    /// <summary>
    /// Helper to retry GET requests and extract the string content.
    /// </summary>
    private static async Task<string> GetStringWithRetryAsync(HttpClient client, string url, int maxRetries = 15)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception)
            {
                if (i == maxRetries - 1)
                {
                    throw;
                }
            }
            await Task.Delay(1000);
        }

        throw new InvalidOperationException("Retry loop failed to fetch the string.");
    }
}