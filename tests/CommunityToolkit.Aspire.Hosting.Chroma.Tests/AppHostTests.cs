using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Chroma.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Chroma_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Chroma_AppHost>>
{
    [Fact]
    public async Task ChromaResourceStartsAndIsHealthy()
    {
        var distributedAppModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var chromaResource = distributedAppModel.Resources.OfType<ChromaResource>().Single();
        
        var rns = fixture.ResourceNotificationService;
        
        await rns.WaitForResourceHealthyAsync(chromaResource.Name).WaitAsync(TimeSpan.FromMinutes(5));
        
        using var httpClient = fixture.CreateHttpClient(chromaResource.Name);

        var response = await httpClient.GetAsync("/api/v1/heartbeat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiServiceCanCreateAndQueryCollections()
    {
        var rns = fixture.ResourceNotificationService;
        
        await rns.WaitForResourceHealthyAsync("apiservice").WaitAsync(TimeSpan.FromMinutes(5));
        
        using var httpClient = fixture.CreateHttpClient("apiservice");

        // Create
        var createResponse = await httpClient.PostAsync("/create", null);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        
        var result = await createResponse.Content.ReadFromJsonAsync<CreateResult>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Collection);

        // Query
        var queryResponse = await httpClient.GetAsync($"/query?collectionName={result.Collection}");
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
    }

    private record CreateResult(string Collection, int Count);
}
