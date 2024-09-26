using Aspire.Hosting;

namespace Aspire.CommunityToolkit.Hosting.Ollama.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void VerifyDefaultModel()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", port: null).AddModel("llama3").WithDefaultModel("llama3");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        Assert.Equal("ollama", resource.Name);

        Assert.Equal("llama3", resource.DefaultModel);
    }

    [Fact]
    public void VerifyCustomModel()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", port: null).AddModel("custom");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        Assert.Equal("ollama", resource.Name);

        Assert.Contains("custom", resource.Models);
    }

    [Fact]
    public void VerifyDefaultPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", port: null);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());

        Assert.Equal(11434, endpoint.TargetPort);
    }

    [Fact]
    public void VerifyCustomPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", port: 12345);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());

        Assert.Equal(12345, endpoint.Port);
    }

    [Fact]
    public void CanSetMultpleModels()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", port: null)
            .AddModel("llama3")
            .AddModel("phi3");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        Assert.Equal("ollama", resource.Name);

        Assert.Contains("llama3", resource.Models);
        Assert.Contains("phi3", resource.Models);
    }
}
