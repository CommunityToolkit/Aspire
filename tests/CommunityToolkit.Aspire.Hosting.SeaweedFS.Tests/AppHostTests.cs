using CommunityToolkit.Aspire.Testing;
using System.Net.Http.Json;
using System.Text;

namespace CommunityToolkit.Aspire.Hosting.SeaweedFS.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.SeaweedFS_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.SeaweedFS_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndApiCanPerformS3Operations()
    {
        const string apiResourceName = "apiservice";
        const string seaweedResourceName = "seaweedfs";

        CancellationTokenSource cts = new(TimeSpan.FromMinutes(3));

        // Wait for both the database container and the API to become fully healthy
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(seaweedResourceName, cts.Token).WaitAsync(cts.Token);
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(apiResourceName, cts.Token).WaitAsync(cts.Token);

        HttpClient httpClient = fixture.CreateHttpClient(apiResourceName);

        // 1. Create a Bucket via the S3 API endpoint (With Retry for Eventual Consistency)
        const string bucketName = "e2e-test-bucket";
        HttpResponseMessage? createBucketResponse = null;

        // Retry loop to handle the S3 Gateway topology mapping delay right after boot
        for (int i = 0; i < 15; i++)
        {
            createBucketResponse = await httpClient.PostAsync($"/s3/buckets?bucketName={bucketName}", null, cts.Token);
            if (createBucketResponse.IsSuccessStatusCode)
            {
                break;
            }
            await Task.Delay(1000, cts.Token); // Waits 1 second before trying again
        }

        Assert.NotNull(createBucketResponse);
        Assert.Equal(HttpStatusCode.OK, createBucketResponse.StatusCode);

        // 2. Upload a file via the S3 API endpoint
        const string s3ObjectKey = "hello-s3.txt";
        const string s3FileContent = "Integration test content via S3";
        StringContent s3StringContent = new($"\"{s3FileContent}\"", Encoding.UTF8, "application/json");

        HttpResponseMessage uploadS3Response = await httpClient.PostAsync($"/s3/upload?bucketName={bucketName}&key={s3ObjectKey}", s3StringContent, cts.Token);
        Assert.Equal(HttpStatusCode.OK, uploadS3Response.StatusCode);

        // 3. Download and verify the file via the S3 API endpoint
        HttpResponseMessage downloadS3Response = await httpClient.GetAsync($"/s3/download?bucketName={bucketName}&key={s3ObjectKey}", cts.Token);
        Assert.Equal(HttpStatusCode.OK, downloadS3Response.StatusCode);

        // Wait, parse and assert the custom anonymous object returned by the API
        S3DownloadResult? s3Result = await downloadS3Response.Content.ReadFromJsonAsync<S3DownloadResult>(cancellationToken: cts.Token);
        Assert.NotNull(s3Result);
        Assert.Equal(s3FileContent, s3Result.Content);
    }

    [Fact]
    public async Task ResourceStartsAndApiCanPerformFilerOperations()
    {
        const string apiResourceName = "apiservice";
        const string seaweedResourceName = "seaweedfs";

        CancellationTokenSource cts = new(TimeSpan.FromMinutes(3));

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(seaweedResourceName, cts.Token).WaitAsync(cts.Token);
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(apiResourceName, cts.Token).WaitAsync(cts.Token);

        HttpClient httpClient = fixture.CreateHttpClient(apiResourceName);

        // 1. Upload a file via the native Filer API endpoint
        const string filerFileName = "hello-filer.txt";
        const string filerFileContent = "Integration test content via Filer";
        StringContent filerStringContent = new($"\"{filerFileContent}\"", Encoding.UTF8, "application/json");

        HttpResponseMessage uploadFilerResponse = await httpClient.PostAsync($"/filer/upload?fileName={filerFileName}", filerStringContent, cts.Token);
        Assert.Equal(HttpStatusCode.OK, uploadFilerResponse.StatusCode);

        // 2. List the root directory of the Filer and ensure the file is there
        HttpResponseMessage listFilerResponse = await httpClient.GetAsync("/filer/list", cts.Token);
        Assert.Equal(HttpStatusCode.OK, listFilerResponse.StatusCode);

        string listResult = await listFilerResponse.Content.ReadAsStringAsync(cts.Token);
        Assert.Contains(filerFileName, listResult);
    }

    // Helper record to parse the API response
    private sealed record S3DownloadResult(string Bucket, string Key, string Content);
}