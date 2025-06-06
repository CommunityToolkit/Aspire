// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using System.Net.Http.Headers;

namespace CommunityToolkit.Aspire.Hosting.Minio.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Apache_Tika_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Apache_Tika_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "tika";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromSeconds(30));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TikaEndpoint()
    {
        const string resourceName = "tika";
        // PDF file from https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf
        var pdfFile = await File.ReadAllBytesAsync("dummy.pdf");

        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName, cts.Token).WaitAsync(cts.Token);
        var httpClient = fixture.CreateHttpClient(resourceName);

        var request = new HttpRequestMessage(HttpMethod.Put, "/tika")
        {
            Content = new ByteArrayContent(pdfFile),
            Headers =
            {
                Accept =
                {
                    new MediaTypeWithQualityHeaderValue("text/plain")
                }
            }
        };
        var createBucketResponse = await httpClient.SendAsync(request).WaitAsync(cts.Token);
        Assert.Equal(HttpStatusCode.OK, createBucketResponse.StatusCode);

        var content = await createBucketResponse.Content.ReadAsStringAsync(cts.Token);
        Assert.Contains("Dummy PDF file", content);
    }
}
