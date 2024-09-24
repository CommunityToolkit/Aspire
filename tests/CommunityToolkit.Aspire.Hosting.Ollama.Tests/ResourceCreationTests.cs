using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Ollama.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void VerifyDefaultModel()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        Assert.Equal("ollama", resource.Name);

        Assert.Equal("llama3", resource.ModelName);
    }

    [Fact]
    public void VerifyCustomModel()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", modelName: "custom");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        Assert.Equal("ollama", resource.Name);

        Assert.Equal("custom", resource.ModelName);
    }

    [Fact]
    public void VerifyDefaultPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama");

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
}
