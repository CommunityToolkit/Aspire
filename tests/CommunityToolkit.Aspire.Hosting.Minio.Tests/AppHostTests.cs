// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using System.Text;

namespace CommunityToolkit.Aspire.Hosting.Minio.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Minio_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Minio_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "apiservice";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    
    [Fact]
    public async Task ResourceStartsAndUiRespondsOk()
    {
        var resourceName = "minio";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName, "console");

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    
    [Fact]
    public async Task ApiServiceCreateData()
    {
        const string resourceName = "apiservice";
        
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("minio", cts.Token).WaitAsync(cts.Token);
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName, cts.Token).WaitAsync(cts.Token);
        var httpClient = fixture.CreateHttpClient(resourceName);
    
        var bucketName = "somebucket";
        var createBucketResponse = await httpClient.PutAsync($"/buckets/{bucketName}", null).WaitAsync(cts.Token);
        Assert.Equal(HttpStatusCode.OK, createBucketResponse.StatusCode);
    
        var getBucketResponse = await httpClient.GetAsync($"/buckets/{bucketName}");
        Assert.Equal(HttpStatusCode.OK, getBucketResponse.StatusCode);

        var uploadedString = "Hello World";
        var bytes = Encoding.UTF8.GetBytes(uploadedString);
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new("plain/text");

        var uploadFileResponse = await httpClient.PostAsync($"/buckets/{bucketName}/fileName/upload", content);
        Assert.Equal(HttpStatusCode.OK, uploadFileResponse.StatusCode);
        
        var downloadFileResponse = await httpClient.GetAsync($"/buckets/{bucketName}/fileName/download");
        Assert.Equal(HttpStatusCode.OK, downloadFileResponse.StatusCode);

        var downloadedString = await downloadFileResponse.Content.ReadAsStringAsync();
        Assert.Equal(uploadedString, downloadedString);
    }
}
