using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using OllamaSharp;

namespace CommunityToolkit.Aspire.Hosting.Ollama.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Ollama_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Ollama_AppHost>>
{
    [Fact]
    public async Task OpenWebUIResourceStartsAndRespondsOk()
    {
        var distributedAppModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var openWebUI = Assert.Single(distributedAppModel.Resources.OfType<OpenWebUIResource>());
        
        var rns = fixture.ResourceNotificationService;
        
        await rns.WaitForResourceHealthyAsync(openWebUI.Name).WaitAsync(TimeSpan.FromMinutes(10));
        
        using var httpClient = fixture.CreateHttpClient(openWebUI.Name);
        
        var response = await httpClient.GetAsync("/");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    
    [Fact]
    public async Task OllamaResourcesStartAndRespondOk()
    {
        var distributedAppModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var ollamaResources = distributedAppModel.Resources.OfType<IOllamaResource>().ToList();
        
        var rns = fixture.ResourceNotificationService;
        
        await Task.WhenAll([
            .. ollamaResources.Select(o => rns.WaitForResourceAsync(o.Name, KnownResourceStates.Running))
        ]).WaitAsync(TimeSpan.FromMinutes(5));
        
        foreach (var ollama in ollamaResources)
        {
            using var httpClient = fixture.CreateHttpClient(ollama.Name);

            var response = await httpClient.GetAsync("/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        } 
    }

    [Fact]
    public async Task OllamaResourcesListAvailableModels()
    {
        var distributedAppModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var ollamaResources = distributedAppModel.Resources.OfType<IOllamaResource>().ToList();
        var modelResources = distributedAppModel.Resources.OfType<OllamaModelResource>().ToList();
        
        var rns = fixture.ResourceNotificationService;

        await Task.WhenAll([
                .. ollamaResources.Select(o => rns.WaitForResourceHealthyAsync(o.Name)),
                .. modelResources.Select(m => rns.WaitForResourceHealthyAsync(m.Name))
            ]).WaitAsync(TimeSpan.FromMinutes(10));
        
        foreach (var ollama in ollamaResources)
        {
            using var httpClient = fixture.CreateHttpClient(ollama.Name);

            var models = (await new OllamaApiClient(httpClient).ListLocalModelsAsync()).ToList();
            var ollamaModels = modelResources.Where(m => m.Parent == ollama).ToList();

            Assert.NotEmpty(models);
            Assert.Equal(ollamaModels.Count, models.Count);
        }
    }
}
