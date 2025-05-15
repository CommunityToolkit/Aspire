// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
    public async Task ApiServiceCreateData()
    {
        var resourceName = "apiservice";
    
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("minio").WaitAsync(TimeSpan.FromMinutes(5));
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);
    
        var bucketName = "somebucket";
        var createResponse = await httpClient.PutAsync($"/buckets/{bucketName}", null).WaitAsync(TimeSpan.FromMinutes(5));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
    
        var getResponse = await httpClient.GetAsync($"/buckets/{bucketName}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }
}
