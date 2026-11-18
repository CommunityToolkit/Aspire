using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Data.Common;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.VLLM.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.VLLM_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.VLLM_AppHost>>
{
    [Fact]
    public async Task VLLMResourceStartsAndServesModels()
    {
        // vLLM only reports healthy once the model has been downloaded and loaded.
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("vllm").WaitAsync(TimeSpan.FromMinutes(15));
        var model = await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync("model")
            .WaitAsync(TimeSpan.FromMinutes(1));
        Assert.Equal(HealthStatus.Healthy, model.Snapshot.HealthStatus);

        await fixture.ResourceNotificationService
            .WaitForResourceAsync("model-consumer", KnownResourceStates.TerminalStates)
            .WaitAsync(TimeSpan.FromMinutes(1));
        Assert.True(fixture.ResourceNotificationService.TryGetCurrentState("model-consumer", out var consumer));
        Assert.Equal(KnownResourceStates.Finished, consumer.Snapshot.State?.Text);
        Assert.Equal(0, consumer.Snapshot.ExitCode);

        var connection = new DbConnectionStringBuilder
        {
            ConnectionString = await fixture.App.GetConnectionStringAsync("model", TestContext.Current.CancellationToken),
        };
        Assert.Equal("opt", connection["Model"]);

        using var httpClient = fixture.CreateHttpClient("vllm");

        var response = await httpClient.GetAsync("/v1/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("facebook/opt-125m", content);
        using var models = JsonDocument.Parse(content);
        Assert.Contains(models.RootElement.GetProperty("data").EnumerateArray(),
            item => item.GetProperty("id").GetString() == (string)connection["Model"]);
    }
}
