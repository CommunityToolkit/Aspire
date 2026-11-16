using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.VLLM.Tests;

[RequiresDocker]
[Collection("VLLM Docker AppHosts")]
public class AppHostTests(AspireIntegrationTestFixture<Projects.VLLM_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.VLLM_AppHost>>
{
    [Fact]
    public async Task VLLMResourceStartsAndServesModels()
    {
        // vLLM only reports healthy once the model has been downloaded and loaded.
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("vllm").WaitAsync(TimeSpan.FromMinutes(15));

        using var httpClient = fixture.CreateHttpClient("vllm");

        var response = await httpClient.GetAsync("/v1/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("facebook/opt-125m", await response.Content.ReadAsStringAsync());
    }
}
