using CommunityToolkit.Aspire.Testing;
using Projects;
using System.Text;

namespace CommunityToolkit.Aspire.Hosting.LlamaCpp.Tests;

[RequiresDocker]
public class LlamaCppIntegrationTests(AspireIntegrationTestFixture<CommunityToolkit_Aspire_Hosting_LlamaCpp_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<CommunityToolkit_Aspire_Hosting_LlamaCpp_AppHost>>
{

    [Fact]
    public async Task LlamaServerStartsOk()
    {
        var resourceName = "llamaserver";
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        using var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LlamaServerHasModel()
    {
        var resourceName = "llamaserver";
        var modelName = "tiny-model";
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        using var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/v1/models");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(modelName, content);
    }
    [Fact]
    public async Task LlamaServerHasPropsAvailable()
    {
        var resourceName = "llamaserver";
        var referenceString= @"""model_alias"":""tiny-model""";
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        using var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/props");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(referenceString, content);
    }
}